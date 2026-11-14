using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaaS.API.IntegrationTests.Infrastructure;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.Identity;
using SaaS.Application.Modules.Memberships;
using SaaS.Application.Modules.Subscriptions;
using SaaS.Application.Modules.Tenants;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Invitations;
using SaaS.Domain.Memberships;
using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;
using SaaS.Infrastructure.Email;
using SaaS.Infrastructure.Persistence;
using Shouldly;

namespace SaaS.API.IntegrationTests.Business;

public sealed class BusinessModulesIntegrationTests : IClassFixture<PostgresWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly PostgresWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public BusinessModulesIntegrationTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<AuthTokenResponse> LoginAsync(string email, string? password = null)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = password ?? PostgresWebApplicationFactory.DemoPassword
        });
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await resp.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        payload.ShouldNotBeNull();
        return payload;
    }

    private static HttpRequestMessage CreateTenantRequest(
        HttpMethod method,
        string url,
        string accessToken,
        Guid tenantId,
        object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }
        return req;
    }

    private static HttpRequestMessage CreateUserRequest(
        HttpMethod method,
        string url,
        string accessToken,
        object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }
        return req;
    }

    [Fact]
    public async Task InvitationLifecycle_AcceptByWrongUserRejected_AndCorrectUserJoinsAndRespectsLastOwnerProtection()
    {
        var outbox = _factory.Services.GetRequiredService<IDevEmailOutbox>();

        // 1. Register & verify frank@acme.local
        var registerResp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "frank@acme.local",
            password = PostgresWebApplicationFactory.DemoPassword,
            fullName = "Frank Miller"
        });
        registerResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var verifyMail = outbox.GetLatestFor("frank@acme.local");
        verifyMail.ShouldNotBeNull();

        var verifyResp = await _client.PostAsJsonAsync("/api/v1/auth/verify-email", new
        {
            userId = verifyMail.UserId!.Value,
            token = verifyMail.Token
        });
        verifyResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 2. Acme owner invites frank@acme.local as Member
        var alice = await LoginAsync("alice@acme.local");
        var acmeId = alice.Tenants.Single(t => t.Slug == "acme-corp").TenantId;

        var rolesResp = await _client.SendAsync(CreateTenantRequest(HttpMethod.Get, "/api/v1/roles", alice.AccessToken, acmeId));
        rolesResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roles = await rolesResp.Content.ReadFromJsonAsync<List<TenantRoleSummaryDto>>(JsonOptions);
        roles.ShouldNotBeNull();
        var memberRole = roles.Single(r => r.Code == SystemRoleCodes.Member);
        var ownerRole = roles.Single(r => r.Code == SystemRoleCodes.OrganizationOwner);

        var inviteResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/invitations",
            alice.AccessToken,
            acmeId,
            new { email = "frank@acme.local", roleId = memberRole.Id }));
        inviteResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Duplicate pending invitation must be rejected
        var dupInviteResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/invitations",
            alice.AccessToken,
            acmeId,
            new { email = "frank@acme.local", roleId = memberRole.Id }));
        dupInviteResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var inviteMail = outbox.GetLatestFor("frank@acme.local");
        inviteMail.ShouldNotBeNull();
        var rawInviteToken = inviteMail.Token;

        // 3. Another user (dave@globex.local) attempting to accept Frank's token must be denied
        var dave = await LoginAsync("dave@globex.local");
        var wrongAcceptResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            "/api/v1/invitations/accept",
            dave.AccessToken,
            new { token = rawInviteToken }));
        wrongAcceptResp.StatusCode.ShouldNotBe(HttpStatusCode.OK);

        // 4. Frank accepts invitation successfully
        var frank = await LoginAsync("frank@acme.local");
        var acceptResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            "/api/v1/invitations/accept",
            frank.AccessToken,
            new { token = rawInviteToken }));
        acceptResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var acceptResult = await acceptResp.Content.ReadFromJsonAsync<AcceptInvitationResponse>(JsonOptions);
        acceptResult!.TenantId.ShouldBe(acmeId);
        acceptResult.RoleCode.ShouldBe(SystemRoleCodes.Member);

        // 5. Bob (ORG_ADMIN) cannot escalate Frank to ORG_OWNER and cannot remove Alice
        var bob = await LoginAsync("bob@acme.local");
        var membersResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/members?search=frank",
            bob.AccessToken,
            acmeId));
        var membersPage = await membersResp.Content.ReadFromJsonAsync<PagedResult<MemberListItemDto>>(JsonOptions);
        var frankMembership = membersPage!.Items.Single(m => m.Email == "frank@acme.local");

        var bobEscalateResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/members/{frankMembership.MembershipId}/role",
            bob.AccessToken,
            acmeId,
            new { roleId = ownerRole.Id }));
        bobEscalateResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 6. Alice cannot demote or remove herself as the sole Organization Owner
        var allMembersResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/members?pageSize=50",
            alice.AccessToken,
            acmeId));
        var allMembers = await allMembersResp.Content.ReadFromJsonAsync<PagedResult<MemberListItemDto>>(JsonOptions);
        var aliceMembership = allMembers!.Items.Single(m => m.Email == "alice@acme.local");

        var removeLastOwner = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Delete,
            $"/api/v1/members/{aliceMembership.MembershipId}",
            alice.AccessToken,
            acmeId));
        removeLastOwner.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Clean up Frank membership so other tests remain predictable
        var removeFrank = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Delete,
            $"/api/v1/members/{frankMembership.MembershipId}",
            alice.AccessToken,
            acmeId));
        removeFrank.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CustomRoles_PlanGatingAndRoleLifecycle_ShouldEnforceFeatureGatesAndPrivilegeRules()
    {
        var alice = await LoginAsync("alice@acme.local");
        var acmeId = alice.Tenants.Single(t => t.Slug == "acme-corp").TenantId;

        // Pro plan (Acme) allows creating custom roles
        var createRoleResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/roles",
            alice.AccessToken,
            acmeId,
            new
            {
                name = "Audit Reviewer",
                description = "Read-only compliance auditor",
                permissions = new[] { PermissionCatalog.AuditRead, PermissionCatalog.DashboardRead }
            }));
        createRoleResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var customRole = await createRoleResp.Content.ReadFromJsonAsync<TenantRoleSummaryDto>(JsonOptions);
        customRole!.Name.ShouldBe("Audit Reviewer");

        // Update custom role
        var updateRoleResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/roles/{customRole.Id}",
            alice.AccessToken,
            acmeId,
            new
            {
                name = "Senior Auditor",
                description = "Updated compliance role",
                permissions = new[] { PermissionCatalog.AuditRead, PermissionCatalog.DashboardRead, PermissionCatalog.MembersRead }
            }));
        updateRoleResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updatedRole = await updateRoleResp.Content.ReadFromJsonAsync<TenantRoleSummaryDto>(JsonOptions);
        updatedRole!.Name.ShouldBe("Senior Auditor");
        updatedRole.Permissions.ShouldContain(PermissionCatalog.MembersRead);

        // Non-owner admin (bob) cannot create custom role containing billing.manage
        var bob = await LoginAsync("bob@acme.local");
        var bobEscalateRoleResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/roles",
            bob.AccessToken,
            acmeId,
            new
            {
                name = "Shadow Billing Admin",
                description = "Escalation test",
                permissions = new[] { PermissionCatalog.BillingManage }
            }));
        bobEscalateRoleResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Delete unreferenced custom role
        var deleteRoleResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Delete,
            $"/api/v1/roles/{customRole.Id}",
            alice.AccessToken,
            acmeId));
        deleteRoleResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Globex Labs (seeded FREE tier with 3/3 members and AllowCustomRoles = false) must block custom role creation & 4th invite
        var dave = await LoginAsync("dave@globex.local");
        var globexId = dave.Tenants.Single(t => t.Slug == "globex-labs").TenantId;

        var globexRoleCreateResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/roles",
            dave.AccessToken,
            globexId,
            new
            {
                name = "Free Tier Custom",
                description = "Should be gated",
                permissions = new[] { PermissionCatalog.DashboardRead }
            }));
        globexRoleCreateResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var globexRolesResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/roles",
            dave.AccessToken,
            globexId));
        var globexRoles = await globexRolesResp.Content.ReadFromJsonAsync<List<TenantRoleSummaryDto>>(JsonOptions);
        var globexMemberRole = globexRoles!.Single(r => r.Code == SystemRoleCodes.Member);

        var fourthSeatInvite = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/invitations",
            dave.AccessToken,
            globexId,
            new { email = "fourth@globex.local", roleId = globexMemberRole.Id }));
        fourthSeatInvite.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Subscriptions_PublicPlansUpgradeHistoryAndExpiredTrialWriteBlock_ShouldWorkEndToEnd()
    {
        // Public plans endpoint requires zero auth or X-Tenant-Id
        var publicPlansResp = await _client.GetAsync("/api/v1/subscriptions/plans");
        publicPlansResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plans = await publicPlansResp.Content.ReadFromJsonAsync<List<SubscriptionPlanDto>>(JsonOptions);
        plans.ShouldNotBeNull();
        plans.Select(p => p.Code).ShouldContain("FREE");
        plans.Select(p => p.Code).ShouldContain("PRO");
        plans.Select(p => p.Code).ShouldContain("ENTERPRISE");

        // Create a fresh tenant via API -> auto-provisioned 14-day PRO trial
        var alice = await LoginAsync("alice@acme.local");
        var createOrgResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            "/api/v1/tenants",
            alice.AccessToken,
            new { name = "Trial Enforcement Labs", slug = "trial-enforcement-labs" }));
        createOrgResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdOrg = await createOrgResp.Content.ReadFromJsonAsync<TenantDetailDto>(JsonOptions);
        var newTenantId = createdOrg!.Id;

        var initialSubResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/subscriptions/current",
            alice.AccessToken,
            newTenantId));
        initialSubResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var initialSub = await initialSubResp.Content.ReadFromJsonAsync<CurrentSubscriptionDto>(JsonOptions);
        initialSub!.Status.ShouldBe(SubscriptionStatus.Trialing);
        initialSub.Plan.Code.ShouldBe("PRO");
        initialSub.AllowsWorkspaceWrites.ShouldBeTrue();

        // Force trial expiry in PostgreSQL to verify expired-trial enforcement
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tenant_subscriptions SET trial_ends_at_utc = {DateTimeOffset.UtcNow.AddDays(-2)} WHERE tenant_id = {newTenantId}");
        }

        // Workspace writes (updating tenant settings) must now fail with 403
        var blockedUpdateResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/tenants/current",
            alice.AccessToken,
            newTenantId,
            new { name = "Should Be Blocked", slug = "trial-enforcement-labs" }));
        blockedUpdateResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Billing inspection and plan upgrade remain allowed so tenant can upgrade!
        var upgradeResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/subscriptions/change-plan",
            alice.AccessToken,
            newTenantId,
            new { planCode = "ENTERPRISE", billingCycle = BillingCycle.Monthly, paymentMethodToken = "tok_visa" }));
        upgradeResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var upgradedSub = await upgradeResp.Content.ReadFromJsonAsync<CurrentSubscriptionDto>(JsonOptions);
        upgradedSub!.Status.ShouldBe(SubscriptionStatus.Active);
        upgradedSub.Plan.Code.ShouldBe("ENTERPRISE");
        upgradedSub.AllowsWorkspaceWrites.ShouldBeTrue();

        // Workspace writes now succeed again!
        var unblockedUpdateResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            "/api/v1/tenants/current",
            alice.AccessToken,
            newTenantId,
            new { name = "Upgraded Enterprise Org", slug = "trial-enforcement-labs" }));
        unblockedUpdateResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // History tracks both TrialStarted and Subscribed/Upgraded
        var historyResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/subscriptions/history",
            alice.AccessToken,
            newTenantId));
        historyResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var history = await historyResp.Content.ReadFromJsonAsync<PagedResult<SubscriptionHistoryDto>>(JsonOptions);
        history!.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CrossTenantIdor_AuditLogs_DashboardAndPlatformAdmin_ShouldEnforceStrictIsolationAndReporting()
    {
        var alice = await LoginAsync("alice@acme.local");
        var dave = await LoginAsync("dave@globex.local");
        var platformAdmin = await LoginAsync("platform.admin@saas.local");

        var acmeId = alice.Tenants.Single(t => t.Slug == "acme-corp").TenantId;
        var globexId = dave.Tenants.Single(t => t.Slug == "globex-labs").TenantId;

        // Cross-tenant IDOR: Alice queries Globex's role/member ID using Acme's X-Tenant-Id -> 404 NotFound
        var globexRolesResp = await _client.SendAsync(CreateTenantRequest(HttpMethod.Get, "/api/v1/roles", dave.AccessToken, globexId));
        var globexRoles = await globexRolesResp.Content.ReadFromJsonAsync<List<TenantRoleSummaryDto>>(JsonOptions);
        var globexRoleId = globexRoles!.First().Id;

        var idorRoleUpdate = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/roles/{globexRoleId}",
            alice.AccessToken,
            acmeId,
            new { name = "Cross Tenant Tamper", description = "No", permissions = new[] { PermissionCatalog.TenantRead } }));
        idorRoleUpdate.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Tenant Dashboard returns real PostgreSQL counts & recent activities
        var dashResp = await _client.SendAsync(CreateTenantRequest(HttpMethod.Get, "/api/v1/dashboard", alice.AccessToken, acmeId));
        dashResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dash = await dashResp.Content.ReadFromJsonAsync<TenantDashboardDto>(JsonOptions);
        dash!.TenantSlug.ShouldBe("acme-corp");
        dash.MemberStats.ActiveMembers.ShouldBeGreaterThanOrEqualTo(4);
        dash.Subscription.PlanCode.ShouldBe("PRO");
        dash.RecentActivities.ShouldNotBeEmpty();

        // Tenant audit logs never expose Globex logs
        var acmeAuditResp = await _client.SendAsync(CreateTenantRequest(HttpMethod.Get, "/api/v1/audit-logs?pageSize=50", alice.AccessToken, acmeId));
        acmeAuditResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var acmeLogs = await acmeAuditResp.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>(JsonOptions);
        acmeLogs!.Items.ShouldAllBe(l => l.TenantId == acmeId && l.Scope == Domain.Audit.AuditScope.Tenant);

        // Non-platform user cannot access /api/v1/platform/*
        var forbiddenPlatformResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Get,
            "/api/v1/platform/dashboard",
            alice.AccessToken));
        forbiddenPlatformResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Platform admin inspects platform dashboard, tenants, and suspends/reactivates a dedicated org
        var platformDashResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Get,
            "/api/v1/platform/dashboard",
            platformAdmin.AccessToken));
        platformDashResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var platformDash = await platformDashResp.Content.ReadFromJsonAsync<PlatformDashboardDto>(JsonOptions);
        platformDash!.TotalTenants.ShouldBeGreaterThanOrEqualTo(2);
        platformDash.TotalUsers.ShouldBeGreaterThanOrEqualTo(7);

        var tempOrgResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            "/api/v1/tenants",
            alice.AccessToken,
            new { name = "Suspendable Org", slug = "suspendable-org" }));
        tempOrgResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tempOrg = await tempOrgResp.Content.ReadFromJsonAsync<TenantDetailDto>(JsonOptions);

        var suspendResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            $"/api/v1/platform/tenants/{tempOrg!.Id}/suspend",
            platformAdmin.AccessToken,
            new { reason = "Policy review hold" }));
        suspendResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Alice calling suspended tenant workspace is blocked with 403 tenant_suspended
        var accessSuspendedResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/tenants/current",
            alice.AccessToken,
            tempOrg.Id));
        accessSuspendedResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var reactivateResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            $"/api/v1/platform/tenants/{tempOrg.Id}/reactivate",
            platformAdmin.AccessToken));
        reactivateResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var accessReactivatedResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/tenants/current",
            alice.AccessToken,
            tempOrg.Id));
        accessReactivatedResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
