using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SaaS.API.IntegrationTests.Infrastructure;
using SaaS.Application.Modules.Identity;
using SaaS.Application.Modules.Tenants;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;
using SaaS.Infrastructure.Persistence;
using Shouldly;

namespace SaaS.API.IntegrationTests.Security;

public sealed class TenantIsolationIntegrationTests : IClassFixture<PostgresWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly PostgresWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TenantIsolationIntegrationTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<AuthTokenResponse> LoginAsAsync(string email)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = PostgresWebApplicationFactory.DemoPassword
        });
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await resp.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        auth.ShouldNotBeNull();
        return auth;
    }

    [Fact]
    public async Task SpoofedTenantHeader_ByUserFromAnotherTenant_ShouldReturn403AndWriteSecurityAuditLog()
    {
        var alice = await LoginAsAsync("alice@acme.local");
        var dave = await LoginAsAsync("dave@globex.local");

        var globexId = dave.Tenants.Single(t => t.Slug == "globex-labs").TenantId;
        alice.Tenants.ShouldNotContain(t => t.TenantId == globexId);

        using var spoofedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenants/current");
        spoofedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        spoofedReq.Headers.Add("X-Tenant-Id", globexId.ToString());

        var response = await _client.SendAsync(spoofedReq);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auditLogged = await db.AuditLogs
            .IgnoreQueryFilters()
            .AnyAsync(a => a.Action == "CROSS_TENANT_ACCESS_DENIED"
                        && a.ActorEmail == "alice@acme.local"
                        && a.TargetId == globexId.ToString());
        auditLogged.ShouldBeTrue();
    }

    [Fact]
    public async Task MissingTenantHeader_OnTenantScopedEndpoint_ShouldReturn400BadRequest()
    {
        var alice = await LoginAsAsync("alice@acme.local");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenants/current");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);

        var response = await _client.SendAsync(req);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MultiTenantUser_SwitchingOrganizations_ShouldReceivePerTenantRoleAndPermissions()
    {
        var consultant = await LoginAsAsync("consultant@demo.local");
        consultant.Tenants.Count.ShouldBe(2);

        var acmeId = consultant.Tenants.Single(t => t.Slug == "acme-corp").TenantId;
        var globexId = consultant.Tenants.Single(t => t.Slug == "globex-labs").TenantId;

        using var acmeReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenants/current");
        acmeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", consultant.AccessToken);
        acmeReq.Headers.Add("X-Tenant-Id", acmeId.ToString());
        var acmeResp = await _client.SendAsync(acmeReq);
        acmeResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var acmeDetail = await acmeResp.Content.ReadFromJsonAsync<TenantDetailDto>(JsonOptions);
        acmeDetail.ShouldNotBeNull();
        acmeDetail.CurrentUserRoleCode.ShouldBe(SystemRoleCodes.Manager);
        acmeDetail.CurrentUserPermissions.ShouldContain(PermissionCatalog.MembersInvite);

        using var globexReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenants/current");
        globexReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", consultant.AccessToken);
        globexReq.Headers.Add("X-Tenant-Id", globexId.ToString());
        var globexResp = await _client.SendAsync(globexReq);
        globexResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var globexDetail = await globexResp.Content.ReadFromJsonAsync<TenantDetailDto>(JsonOptions);
        globexDetail.ShouldNotBeNull();
        globexDetail.CurrentUserRoleCode.ShouldBe(SystemRoleCodes.Member);
        globexDetail.CurrentUserPermissions.ShouldNotContain(PermissionCatalog.MembersInvite);
    }

    [Fact]
    public async Task RbacPermissionEnforcement_MemberCannotUpdateTenantOrSettings_AdminCan()
    {
        var carol = await LoginAsAsync("carol@acme.local");
        var bob = await LoginAsAsync("bob@acme.local");
        var acmeId = carol.Tenants.Single(t => t.Slug == "acme-corp").TenantId;

        // Carol (MEMBER) attempts settings read/update -> 403
        using var carolSettingsReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenants/current/settings");
        carolSettingsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", carol.AccessToken);
        carolSettingsReq.Headers.Add("X-Tenant-Id", acmeId.ToString());
        var carolResp = await _client.SendAsync(carolSettingsReq);
        carolResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Bob (ORG_ADMIN) reads & updates settings -> 200
        using var bobUpdateReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/tenants/current/settings")
        {
            Content = JsonContent.Create(new
            {
                billingEmail = "billing-updated@acme.local",
                supportEmail = "support@acme.local",
                timezone = "America/New_York",
                locale = "en-US",
                enforceEmailDomain = true,
                allowedEmailDomains = new[] { "acme.local" },
                sessionTimeoutMinutes = 480
            })
        };
        bobUpdateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bob.AccessToken);
        bobUpdateReq.Headers.Add("X-Tenant-Id", acmeId.ToString());
        var bobResp = await _client.SendAsync(bobUpdateReq);
        bobResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updatedSettings = await bobResp.Content.ReadFromJsonAsync<TenantSettingsDto>(JsonOptions);
        updatedSettings.ShouldNotBeNull();
        updatedSettings.BillingEmail.ShouldBe("billing-updated@acme.local");
        updatedSettings.EnforceEmailDomain.ShouldBeTrue();
        updatedSettings.AllowedEmailDomains.ShouldContain("acme.local");
    }

    [Fact]
    public async Task CreateTenant_ShouldProvisionAtomicWorkspaceAndBlockSuspendedTenantAccess()
    {
        var alice = await LoginAsAsync("alice@acme.local");
        var uniqueSlug = $"wayne-{Guid.NewGuid():N}"[..18];

        using var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants")
        {
            Content = JsonContent.Create(new { name = "Wayne Enterprises", slug = uniqueSlug })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);

        var createResp = await _client.SendAsync(createReq);
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<TenantDetailDto>(JsonOptions);
        created.ShouldNotBeNull();
        created.Slug.ShouldBe(uniqueSlug);
        created.CurrentUserRoleCode.ShouldBe(SystemRoleCodes.OrganizationOwner);

        // Complete onboarding
        using var onboardReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants/current/complete-onboarding");
        onboardReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        onboardReq.Headers.Add("X-Tenant-Id", created.Id.ToString());
        var onboardResp = await _client.SendAsync(onboardReq);
        onboardResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Suspend this newly created test tenant directly in DB and verify workspace access is blocked
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await db.Tenants.SingleAsync(t => t.Id == created.Id);
            tenant.Suspend("Compliance review", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        using var afterSuspendReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tenants/current");
        afterSuspendReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        afterSuspendReq.Headers.Add("X-Tenant-Id", created.Id.ToString());
        var afterSuspendResp = await _client.SendAsync(afterSuspendReq);
        afterSuspendResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostgreSqlCompositeForeignKey_ShouldRejectCrossTenantRoleAssignmentAtDatabaseLayer()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var acme = await db.Tenants.AsNoTracking().SingleAsync(t => t.Slug == Domain.Tenants.TenantSlug.Create("acme-corp"));
        var globex = await db.Tenants.AsNoTracking().SingleAsync(t => t.Slug == Domain.Tenants.TenantSlug.Create("globex-labs"));

        var globexRole = await db.TenantRoles.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(r => r.TenantId == globex.Id);
        var daveUser = await db.Users.AsNoTracking().SingleAsync(u => u.Email == "dave@globex.local");

        // Attempt to insert a membership in Acme pointing to a Role owned by Globex
        var illegalMembership = new TenantMembership(acme.Id, daveUser.Id, globexRole.Id, DateTimeOffset.UtcNow);
        await db.TenantMemberships.AddAsync(illegalMembership);

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pgEx = ex.InnerException.ShouldBeOfType<PostgresException>();
        pgEx.SqlState.ShouldBe("23503");
    }
}
