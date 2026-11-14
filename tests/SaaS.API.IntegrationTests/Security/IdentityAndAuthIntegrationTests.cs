using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaaS.API.IntegrationTests.Infrastructure;
using SaaS.Application.Modules.Identity;
using SaaS.Infrastructure.Email;
using SaaS.Infrastructure.Persistence;
using Shouldly;

namespace SaaS.API.IntegrationTests.Security;

public sealed class IdentityAndAuthIntegrationTests : IClassFixture<PostgresWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly PostgresWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IdentityAndAuthIntegrationTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_VerifyEmail_Login_And_Me_ShouldSucceedEndToEnd()
    {
        var email = $"newuser.{Guid.NewGuid():N}@example.local";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "StrongPassword123!",
            fullName = "New Test User"
        });
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterUserResponse>(JsonOptions);
        registered.ShouldNotBeNull();
        registered.Email.ShouldBe(email);
        registered.EmailVerificationRequired.ShouldBeTrue();

        // Unverified login must be rejected cleanly
        var unverifiedLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "StrongPassword123!"
        });
        unverifiedLogin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Fetch verification token from dev email outbox
        var outbox = _factory.Services.GetRequiredService<IDevEmailOutbox>();
        var emailMsg = outbox.GetLatestFor(email);
        emailMsg.ShouldNotBeNull();

        var verifyResponse = await _client.PostAsJsonAsync("/api/v1/auth/verify-email", new
        {
            userId = registered.UserId,
            token = emailMsg.Token
        });
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now login succeeds
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "StrongPassword123!"
        });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        auth.ShouldNotBeNull();
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var meResponse = await _client.SendAsync(req);

        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions);
        me.ShouldNotBeNull();
        me.Email.ShouldBe(email);
        me.EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshTokenRotation_WhenOldTokenReplayed_ShouldRevokeEntireFamilyAndSession()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "bob@acme.local",
            password = PostgresWebApplicationFactory.DemoPassword
        });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var initialAuth = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        initialAuth.ShouldNotBeNull();

        // Legitimate rotation
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = initialAuth.RefreshToken
        });
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotatedAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        rotatedAuth.ShouldNotBeNull();
        rotatedAuth.RefreshToken.ShouldNotBe(initialAuth.RefreshToken);

        // Attack: replay old revoked refresh token
        var replayResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = initialAuth.RefreshToken
        });
        replayResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Newer refresh token in the same family must ALSO now be revoked
        var legitimateRetryAfterCompromise = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = rotatedAuth.RefreshToken
        });
        legitimateRetryAfterCompromise.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Active access token bound to the revoked session must ALSO now fail
        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rotatedAuth.AccessToken);
        var meAfterRevoke = await _client.SendAsync(meReq);
        meAfterRevoke.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Verify security audit log persisted
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var securityAuditExists = await db.AuditLogs
            .IgnoreQueryFilters()
            .AnyAsync(a => a.Action == "REFRESH_TOKEN_REUSE_DETECTED" && a.ActorEmail == "bob@acme.local");
        securityAuditExists.ShouldBeTrue();
    }

    [Fact]
    public async Task Sessions_CrossUserSessionRevocation_ShouldReturn404NotFound()
    {
        var aliceLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "alice@acme.local",
            password = PostgresWebApplicationFactory.DemoPassword
        });
        var aliceAuth = await aliceLogin.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        aliceAuth.ShouldNotBeNull();

        using var aliceSessionsReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/sessions");
        aliceSessionsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aliceAuth.AccessToken);
        var aliceSessionsResp = await _client.SendAsync(aliceSessionsReq);
        aliceSessionsResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var aliceSessions = await aliceSessionsResp.Content.ReadFromJsonAsync<List<UserSessionDto>>(JsonOptions);
        aliceSessions.ShouldNotBeNull();
        aliceSessions.ShouldNotBeEmpty();

        var daveLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "dave@globex.local",
            password = PostgresWebApplicationFactory.DemoPassword
        });
        var daveAuth = await daveLogin.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        daveAuth.ShouldNotBeNull();

        // Dave attempts to revoke Alice's session ID (IDOR)
        using var idorReq = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/auth/sessions/{aliceSessions[0].Id}");
        idorReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", daveAuth.AccessToken);
        var idorResp = await _client.SendAsync(idorReq);

        idorResp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PasswordReset_ShouldUpdatePasswordAndRevokeExistingSessions()
    {
        var email = $"reset.{Guid.NewGuid():N}@example.local";
        var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "InitialPassword123!",
            fullName = "Reset User"
        });
        var reg = await regResp.Content.ReadFromJsonAsync<RegisterUserResponse>(JsonOptions);
        var outbox = _factory.Services.GetRequiredService<IDevEmailOutbox>();
        var verifyMsg = outbox.GetLatestFor(email)!;

        await _client.PostAsJsonAsync("/api/v1/auth/verify-email", new
        {
            userId = reg!.UserId,
            token = verifyMsg.Token
        });

        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "InitialPassword123!"
        });
        var initialAuth = await loginResp.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        initialAuth.ShouldNotBeNull();

        var forgotResp = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        forgotResp.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var resetMsg = outbox.GetLatestFor(email)!;
        var resetResp = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email,
            token = resetMsg.Token,
            newPassword = "NewStrongPassword456!"
        });
        resetResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Old refresh token and session must be revoked
        var oldRefreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = initialAuth.RefreshToken
        });
        oldRefreshResp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // New password succeeds
        var newLoginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "NewStrongPassword456!"
        });
        newLoginResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
