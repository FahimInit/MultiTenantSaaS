using FluentValidation;
using NSubstitute;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Application.Modules.Identity;
using SaaS.Application.Modules.Tenants;
using SaaS.Domain.Audit;
using SaaS.Domain.Identity;
using Shouldly;

namespace SaaS.Application.UnitTests.Modules;

public sealed class IdentityAndTenantHandlerTests
{
    [Fact]
    public async Task RegisterUserHandler_WhenEmailAlreadyExists_ShouldThrowConflictAppException()
    {
        var identityService = Substitute.For<IIdentityService>();
        identityService.FindByEmailAsync("alice@acme.local", Arg.Any<CancellationToken>())
            .Returns(new UserAccountDto(Guid.CreateVersion7(), "alice@acme.local", "Alice", true, true, null));

        var handler = new RegisterUserHandler(
            new RegisterUserValidator(),
            identityService,
            Substitute.For<IEmailService>(),
            Substitute.For<IAuditLogger>());

        await Should.ThrowAsync<ConflictAppException>(() =>
            handler.HandleAsync(new RegisterUserCommand("Alice@Acme.Local", "Str0ngPass!", "Alice")));
    }

    [Fact]
    public async Task RefreshTokenHandler_WhenTokenAlreadyRevoked_ShouldRevokeFamilyAndThrowUnauthorized()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var familyId = Guid.CreateVersion7();

        var revokedToken = new RefreshToken(userId, sessionId, familyId, "HASH_REPLAYED", now.AddHours(-2), now.AddDays(7));
        revokedToken.Revoke("Rotated", now.AddHours(-1), Guid.CreateVersion7());

        var sessionStore = Substitute.For<ISessionStore>();
        sessionStore.FindRefreshTokenByHashAsync("HASH_REPLAYED", Arg.Any<CancellationToken>())
            .Returns(revokedToken);

        var jwtGenerator = Substitute.For<IJwtTokenGenerator>();
        jwtGenerator.HashRefreshToken("RAW_REPLAYED").Returns("HASH_REPLAYED");

        var identityService = Substitute.For<IIdentityService>();
        identityService.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserAccountDto(userId, "alice@acme.local", "Alice", true, true, null));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var auditLogger = Substitute.For<IAuditLogger>();

        var handler = new RefreshTokenHandler(
            new RefreshTokenValidator(),
            sessionStore,
            identityService,
            jwtGenerator,
            Substitute.For<ITenantStore>(),
            unitOfWork,
            dateTimeProvider,
            auditLogger);

        var ex = await Should.ThrowAsync<UnauthorizedAppException>(() =>
            handler.HandleAsync(new RefreshTokenCommand("RAW_REPLAYED")));

        ex.ErrorCode.ShouldBe("refresh_token_reuse_detected");
        await sessionStore.Received(1).RevokeTokenFamilyAndSessionAsync(
            familyId, sessionId, "Refresh token reuse detected", now, Arg.Any<CancellationToken>());
        await auditLogger.Received(1).LogPlatformAsync(
            userId,
            "alice@acme.local",
            AuditCategory.Security,
            "REFRESH_TOKEN_REUSE_DETECTED",
            "UserSession",
            sessionId.ToString(),
            Arg.Any<string>(),
            null,
            Arg.Any<IReadOnlyDictionary<string, object?>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTenantHandler_WhenSlugAlreadyTaken_ShouldThrowConflictAppException()
    {
        var userId = Guid.CreateVersion7();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);

        var identityService = Substitute.For<IIdentityService>();
        identityService.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserAccountDto(userId, "founder@newco.local", "Founder", true, true, null));

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.SlugExistsAsync("acme-corp", null, Arg.Any<CancellationToken>()).Returns(true);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        var handler = new CreateTenantHandler(
            new CreateTenantValidator(),
            currentUser,
            identityService,
            tenantStore,
            Substitute.For<IUnitOfWork>(),
            dateTimeProvider,
            Substitute.For<IAuditLogger>());

        var ex = await Should.ThrowAsync<ConflictAppException>(() =>
            handler.HandleAsync(new CreateTenantCommand("Acme Clone", "acme-corp")));

        ex.ErrorCode.ShouldBe("duplicate_tenant_slug");
    }
}
