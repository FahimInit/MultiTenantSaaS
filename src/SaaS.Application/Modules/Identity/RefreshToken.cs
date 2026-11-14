using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;
using SaaS.Domain.Identity;

namespace SaaS.Application.Modules.Identity;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthTokenResponse>;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshTokenHandler : ICommandHandler<RefreshTokenCommand, AuthTokenResponse>
{
    private readonly IValidator<RefreshTokenCommand> _validator;
    private readonly ISessionStore _sessionStore;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITenantStore _tenantStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public RefreshTokenHandler(
        IValidator<RefreshTokenCommand> validator,
        ISessionStore sessionStore,
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        ITenantStore tenantStore,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _sessionStore = sessionStore;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _tenantStore = tenantStore;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<AuthTokenResponse> HandleAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        var incomingHash = _jwtTokenGenerator.HashRefreshToken(command.RefreshToken.Trim());
        var existingToken = await _sessionStore.FindRefreshTokenByHashAsync(incomingHash, cancellationToken)
            ?? throw new UnauthorizedAppException("Invalid refresh token.", "invalid_refresh_token");

        if (existingToken.IsRevoked)
        {
            await _sessionStore.RevokeTokenFamilyAndSessionAsync(
                existingToken.TokenFamilyId,
                existingToken.SessionId,
                "Refresh token reuse detected",
                now,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var compromisedUser = await _identityService.FindByIdAsync(existingToken.UserId, cancellationToken);
            await _auditLogger.LogPlatformAsync(
                existingToken.UserId,
                compromisedUser?.Email ?? "unknown",
                AuditCategory.Security,
                "REFRESH_TOKEN_REUSE_DETECTED",
                "UserSession",
                existingToken.SessionId.ToString(),
                "Previously rotated refresh token was replayed; revoked entire token family and session.",
                metadata: new Dictionary<string, object?> { ["tokenFamilyId"] = existingToken.TokenFamilyId },
                cancellationToken: cancellationToken);

            throw new UnauthorizedAppException(
                "Refresh token reuse detected. Your session has been terminated for security.",
                "refresh_token_reuse_detected");
        }

        if (existingToken.IsExpired(now))
        {
            existingToken.Revoke("Expired", now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException("Refresh token has expired.", "refresh_token_expired");
        }

        var session = await _sessionStore.FindSessionByIdAsync(existingToken.SessionId, cancellationToken);
        if (session is null || !session.IsActive)
        {
            throw new UnauthorizedAppException("Session is no longer active.", "session_revoked");
        }

        var user = await _identityService.FindByIdAsync(existingToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            await _sessionStore.RevokeTokenFamilyAndSessionAsync(
                existingToken.TokenFamilyId,
                existingToken.SessionId,
                "Account inactive",
                now,
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException("User account is unavailable.", "account_inactive");
        }

        var nextRefreshPair = _jwtTokenGenerator.GenerateRefreshToken();
        var nextRefreshToken = new RefreshToken(
            user.Id,
            session.Id,
            existingToken.TokenFamilyId,
            nextRefreshPair.TokenHash,
            now,
            nextRefreshPair.ExpiresAtUtc);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            existingToken.Revoke("Rotated", now, nextRefreshToken.Id);
            await _sessionStore.AddRefreshTokenAsync(nextRefreshToken, ct);
            session.Touch(now);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        var nextAccess = _jwtTokenGenerator.GenerateAccessToken(user, session.Id);
        var tenants = await _tenantStore.ListUserTenantsAsync(user.Id, cancellationToken);

        return new AuthTokenResponse(
            nextAccess.Token,
            nextAccess.ExpiresAtUtc,
            nextRefreshPair.RawToken,
            nextRefreshPair.ExpiresAtUtc,
            new AuthenticatedUserDto(user.Id, user.Email, user.FullName, user.PlatformRole),
            tenants);
    }
}
