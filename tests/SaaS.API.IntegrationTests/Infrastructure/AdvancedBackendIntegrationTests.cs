using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.Identity;
using SaaS.Application.Modules.Memberships;
using SaaS.Application.Modules.Roles;
using SaaS.Application.Modules.Tenants;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Audit;
using SaaS.Domain.Invitations;
using SaaS.Domain.Subscriptions;
using SaaS.Infrastructure.Caching;
using SaaS.Infrastructure.MultiTenancy;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Persistence.Seed;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace SaaS.API.IntegrationTests.Infrastructure;

public sealed class AdvancedBackendIntegrationTests
    : IClassFixture<AdvancedBackendIntegrationTests.RedisPostgresWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly RedisPostgresWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdvancedBackendIntegrationTests(RedisPostgresWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<AuthTokenResponse> LoginAsync(string email)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = RedisPostgresWebApplicationFactory.DemoPassword
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
    public async Task RedisCache_RoleChangeAndTenantSuspension_ShouldInvalidateCachedPermissionsImmediately()
    {
        // Verify readiness probe confirms both PostgreSQL and Redis distributed cache are healthy.
        var readyResp = await _client.GetAsync("/health/ready");
        readyResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var distributedCache = _factory.Services.GetRequiredService<IDistributedCache>();
        distributedCache.ShouldBeAssignableTo<RedisCache>();
        var cacheService = _factory.Services.GetRequiredService<ICacheService>();

        var alice = await LoginAsync("alice@acme.local");
        var carol = await LoginAsync("carol@acme.local");
        var platformAdmin = await LoginAsync("platform.admin@saas.local");
        var acmeId = alice.Tenants.Single(t => t.Slug == "acme-corp").TenantId;

        // 1. Carol (seeded Member) lacks audit.read -> 403 Forbidden (populates Redis cache entry)
        var initialCarolResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/audit-logs",
            carol.AccessToken,
            acmeId));
        initialCarolResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var epochKey = DistributedCacheService.BuildTenantEpochKey(acmeId);
        var epochBefore = await cacheService.GetAsync<string>(epochKey);
        epochBefore.ShouldNotBeNullOrWhiteSpace();

        var cachedMembership = await cacheService.GetAsync<ResolvedTenantMembership>(
            DistributedCacheService.BuildMembershipKey(acmeId, epochBefore, carol.User.Id));
        cachedMembership.ShouldNotBeNull();
        cachedMembership.Permissions.ShouldNotContain(PermissionCatalog.AuditRead);

        // 2. Alice creates custom role granting audit.read & assigns it to Carol -> target user cache key evicted immediately
        var createRoleResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/roles",
            alice.AccessToken,
            acmeId,
            new
            {
                name = "Redis Cache Tester",
                description = "Tests synchronous permission cache eviction",
                permissions = new[] { PermissionCatalog.DashboardRead, PermissionCatalog.AuditRead }
            }));
        createRoleResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var customRole = await createRoleResp.Content.ReadFromJsonAsync<TenantRoleSummaryDto>(JsonOptions);
        customRole.ShouldNotBeNull();

        var membersListResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/members?search=carol",
            alice.AccessToken,
            acmeId));
        var membersPage = await membersListResp.Content.ReadFromJsonAsync<PagedResult<MemberListItemDto>>(JsonOptions);
        var carolMember = membersPage!.Items.Single(m => m.Email == "carol@acme.local");
        var originalCarolRoleId = carolMember.RoleId;

        var promoteCarolResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/members/{carolMember.MembershipId}/role",
            alice.AccessToken,
            acmeId,
            new { roleId = customRole.Id }));
        promoteCarolResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Carol's immediate next request sees audit.read without waiting for the 2-minute TTL
        var carolAllowedResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/audit-logs",
            carol.AccessToken,
            acmeId));
        carolAllowedResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 3. Alice removes audit.read from the custom role -> O(1) epoch rotation in Redis
        var updateRoleResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/roles/{customRole.Id}",
            alice.AccessToken,
            acmeId,
            new
            {
                name = "Redis Cache Tester",
                description = "Removed audit.read",
                permissions = new[] { PermissionCatalog.DashboardRead }
            }));
        updateRoleResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var epochAfter = await cacheService.GetAsync<string>(epochKey);
        epochAfter.ShouldNotBeNullOrWhiteSpace();
        epochAfter.ShouldNotBe(epochBefore);

        // Carol is immediately blocked again on her next HTTP request
        var carolDeniedAgainResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/audit-logs",
            carol.AccessToken,
            acmeId));
        carolDeniedAgainResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Restore Carol's seeded role and clean up custom role
        var restoreResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Put,
            $"/api/v1/members/{carolMember.MembershipId}/role",
            alice.AccessToken,
            acmeId,
            new { roleId = originalCarolRoleId }));
        restoreResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteRoleResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Delete,
            $"/api/v1/roles/{customRole.Id}",
            alice.AccessToken,
            acmeId));
        deleteRoleResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 4. Platform suspension invalidates cached tenant status immediately
        var tempOrgResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            "/api/v1/tenants",
            alice.AccessToken,
            new { name = "Redis Suspend Lab", slug = "redis-suspend-lab" }));
        tempOrgResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tempOrg = await tempOrgResp.Content.ReadFromJsonAsync<TenantDetailDto>(JsonOptions);

        // Warm Redis membership cache for tempOrg
        var warmResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/tenants/current",
            alice.AccessToken,
            tempOrg!.Id));
        warmResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var suspendResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            $"/api/v1/platform/tenants/{tempOrg.Id}/suspend",
            platformAdmin.AccessToken,
            new { reason = "Redis invalidation verification" }));
        suspendResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var blockedAfterSuspend = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/tenants/current",
            alice.AccessToken,
            tempOrg.Id));
        blockedAfterSuspend.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var reactivateResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            $"/api/v1/platform/tenants/{tempOrg.Id}/reactivate",
            platformAdmin.AccessToken));
        reactivateResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var restoredResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/tenants/current",
            alice.AccessToken,
            tempOrg.Id));
        restoredResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BufferedUsageAndMaintenanceRunner_ShouldFlushApiCallsAndExpireOverdueItemsWithTenantAudit()
    {
        var alice = await LoginAsync("alice@acme.local");
        var consultant = await LoginAsync("consultant@demo.local");

        var createOrgResp = await _client.SendAsync(CreateUserRequest(
            HttpMethod.Post,
            "/api/v1/tenants",
            alice.AccessToken,
            new { name = "Maintenance Verification Org", slug = "maintenance-verification-org" }));
        createOrgResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var org = await createOrgResp.Content.ReadFromJsonAsync<TenantDetailDto>(JsonOptions);
        var orgId = org!.Id;

        // Trigger 3 tenant-scoped API calls buffered in memory
        for (var i = 0; i < 3; i++)
        {
            var dashResp = await _client.SendAsync(CreateTenantRequest(
                HttpMethod.Get,
                "/api/v1/dashboard",
                alice.AccessToken,
                orgId));
            dashResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Create a pending invitation to expire
        var rolesResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/roles",
            alice.AccessToken,
            orgId));
        var roles = await rolesResp.Content.ReadFromJsonAsync<List<TenantRoleSummaryDto>>(JsonOptions);
        var memberRole = roles!.Single(r => r.Code == SystemRoleCodes.Member);

        var inviteResp = await _client.SendAsync(CreateTenantRequest(
            HttpMethod.Post,
            "/api/v1/invitations",
            alice.AccessToken,
            orgId,
            new { email = "overdue.invitee@acme.local", roleId = memberRole.Id }));
        inviteResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var now = DateTimeOffset.UtcNow;

        // Backdate invitation, trial end, and an old refresh token directly in PostgreSQL
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.TenantInvitations
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == orgId && i.Status == InvitationStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAtUtc, now.AddDays(-2)));

            await db.TenantSubscriptions
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == orgId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.TrialEndsAtUtc, now.AddDays(-2)));

            await db.RefreshTokens
                .Where(t => t.UserId == consultant.User.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAtUtc, now.AddDays(-35)));
        }

        var runner = _factory.Services.GetRequiredService<IPlatformMaintenanceRunner>();
        var result = await runner.RunOnceAsync();

        result.ExpiredInvitationsCount.ShouldBeGreaterThanOrEqualTo(1);
        result.ExpiredTrialsCount.ShouldBeGreaterThanOrEqualTo(1);
        result.PurgedRefreshTokensCount.ShouldBeGreaterThanOrEqualTo(1);

        // Verify persisted usage snapshot, status transitions, and tenant-scoped audit logs
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var snapshot = await db.TenantUsageSnapshots
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.TenantId == orgId);
            snapshot.ShouldNotBeNull();
            snapshot.ApiCallsCount.ShouldBeGreaterThanOrEqualTo(3);

            var expiredInvite = await db.TenantInvitations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstAsync(i => i.TenantId == orgId);
            expiredInvite.Status.ShouldBe(InvitationStatus.Expired);

            var expiredSub = await db.TenantSubscriptions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstAsync(s => s.TenantId == orgId);
            expiredSub.Status.ShouldBe(SubscriptionStatus.Expired);

            var expiredHistoryExists = await db.SubscriptionHistory
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(h => h.TenantId == orgId && h.EventType == SubscriptionEventType.Expired);
            expiredHistoryExists.ShouldBeTrue();

            var auditActions = await db.AuditLogs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => a.TenantId == orgId)
                .Select(a => a.Action)
                .ToListAsync();
            auditActions.ShouldContain("INVITATION_EXPIRED");
            auditActions.ShouldContain("TRIAL_EXPIRED");

            var remainingConsultantTokens = await db.RefreshTokens
                .AsNoTracking()
                .CountAsync(t => t.UserId == consultant.User.Id);
            remainingConsultantTokens.ShouldBe(0);
        }
    }

    [Fact]
    public async Task RateLimiting_NoisyTenantExceedingLimit_ShouldNotThrottleOtherTenantsAndEnforceAuthLimit()
    {
        var alice = await LoginAsync("alice@acme.local");
        var dave = await LoginAsync("dave@globex.local");
        var acmeId = alice.Tenants.Single(t => t.Slug == "acme-corp").TenantId;
        var globexId = dave.Tenants.Single(t => t.Slug == "globex-labs").TenantId;

        using var rateLimitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:ApiPermitLimit", "3");
            builder.UseSetting("RateLimiting:ApiWindowSeconds", "60");
            builder.UseSetting("RateLimiting:AuthPermitLimit", "4");
            builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
        });
        using var rateClient = rateLimitedFactory.CreateClient();

        // Tenant 1 (Acme) consumes all 3 permits
        for (var i = 0; i < 3; i++)
        {
            var okResp = await rateClient.SendAsync(CreateTenantRequest(
                HttpMethod.Get,
                "/api/v1/dashboard",
                alice.AccessToken,
                acmeId));
            okResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // 4th request from Tenant 1 must be throttled with RFC 9457 ProblemDetails
        var throttledResp = await rateClient.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/dashboard",
            alice.AccessToken,
            acmeId));
        throttledResp.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        throttledResp.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        throttledResp.Headers.Contains("Retry-After").ShouldBeTrue();

        var problemJson = await throttledResp.Content.ReadFromJsonAsync<JsonElement>();
        problemJson.GetProperty("errorCode").GetString().ShouldBe("rate_limit_exceeded");

        // Tenant 2 (Globex) has its own partition and stays unaffected
        var globexResp = await rateClient.SendAsync(CreateTenantRequest(
            HttpMethod.Get,
            "/api/v1/dashboard",
            dave.AccessToken,
            globexId));
        globexResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Auth endpoint enforces tighter IP-based limit without locking out valid user passwords
        for (var i = 0; i < 4; i++)
        {
            var authOkResp = await rateClient.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = "consultant@demo.local",
                password = RedisPostgresWebApplicationFactory.DemoPassword
            });
            authOkResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var authThrottledResp = await rateClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "consultant@demo.local",
            password = RedisPostgresWebApplicationFactory.DemoPassword
        });
        authThrottledResp.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var authProblem = await authThrottledResp.Content.ReadFromJsonAsync<JsonElement>();
        authProblem.GetProperty("errorCode").GetString().ShouldBe("rate_limit_exceeded");
    }

    [Fact]
    public async Task DistributedCacheService_WhenCacheBackendFails_ShouldGracefullyFallBackToDatabase()
    {
        var alice = await LoginAsync("alice@acme.local");
        var acmeId = alice.Tenants.Single(t => t.Slug == "acme-corp").TenantId;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var faultyCache = new DistributedCacheService(
            new ThrowingDistributedCache(),
            NullLogger<DistributedCacheService>.Instance);
        var resolver = new CachedTenantMembershipResolver(
            new TenantMembershipResolver(db),
            faultyCache);

        var resolved = await resolver.ResolveAsync(alice.User.Id, acmeId);

        resolved.ShouldNotBeNull();
        resolved.TenantId.ShouldBe(acmeId);
        resolved.RoleCode.ShouldBe(SystemRoleCodes.OrganizationOwner);
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new TimeoutException("Simulated Redis outage");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new TimeoutException("Simulated Redis outage");
        public void Refresh(string key) => throw new TimeoutException("Simulated Redis outage");
        public Task RefreshAsync(string key, CancellationToken token = default) =>
            throw new TimeoutException("Simulated Redis outage");
        public void Remove(string key) => throw new TimeoutException("Simulated Redis outage");
        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw new TimeoutException("Simulated Redis outage");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new TimeoutException("Simulated Redis outage");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new TimeoutException("Simulated Redis outage");
    }

    public sealed class RedisPostgresWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public const string DemoPassword = "DemoPass123!";

        private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("saas_platform_adv_test")
            .WithUsername("saas_test")
            .WithPassword("saas_test_pw_local")
            .Build();

        private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7-alpine")
            .Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", _postgresContainer.GetConnectionString());
            builder.UseSetting("Redis:Enabled", "true");
            builder.UseSetting("Redis:ConnectionString", _redisContainer.GetConnectionString());
            builder.UseSetting("Jwt:Issuer", "https://api.saas.test");
            builder.UseSetting("Jwt:Audience", "https://app.saas.test");
            builder.UseSetting("Jwt:SigningKey", "integration-test-only-signing-key-min-32-chars");
            builder.UseSetting("Jwt:AccessTokenMinutes", "15");
            builder.UseSetting("Jwt:RefreshTokenDays", "14");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
            builder.UseSetting("SeedDemoData:Enabled", "true");
            builder.UseSetting("SeedDemoData:RunOnStartup", "false");
            builder.UseSetting("SeedDemoData:DefaultPassword", DemoPassword);
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                _postgresContainer.StartAsync(),
                _redisContainer.StartAsync());

            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync();
            await DatabaseSeeder.SeedDemoDataAsync(Services);
        }

        public new async Task DisposeAsync()
        {
            await base.DisposeAsync();
            await Task.WhenAll(
                _postgresContainer.DisposeAsync().AsTask(),
                _redisContainer.DisposeAsync().AsTask());
        }
    }
}
