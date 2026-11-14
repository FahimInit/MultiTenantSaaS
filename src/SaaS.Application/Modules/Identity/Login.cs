using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;
using SaaS.Domain.Identity;

namespace SaaS.Application.Modules.Identity;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthTokenResponse>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler : ICommandHandler<LoginCommand, AuthTokenResponse>
{
    private readonly IValidator<LoginCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ISessionStore _sessionStore;
    private readonly ITenantStore _tenantStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRequestContextInfo _requestContextInfo;
    private readonly IAuditLogger _auditLogger;

    public LoginHandler(
        IValidator<LoginCommand> validator,
        IIdentityService identityService,
        IJwtTokenGenerator jwtTokenGenerator,
        ISessionStore sessionStore,
        ITenantStore tenantStore,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IRequestContextInfo requestContextInfo,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _sessionStore = sessionStore;
        _tenantStore = tenantStore;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _requestContextInfo = requestContextInfo;
        _auditLogger = auditLogger;
    }

    public async Task<AuthTokenResponse> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var check = await _identityService.CheckPasswordSignInAsync(
            normalizedEmail,
            command.Password,
            cancellationToken);

        if (check.Status == CredentialCheckStatus.LockedOut)
        {
            await _auditLogger.LogPlatformAsync(
                check.User?.Id,
                normalizedEmail,
                AuditCategory.Security,
                "LOGIN_LOCKED_OUT",
                "User",
                check.User?.Id.ToString() ?? normalizedEmail,
                "Login blocked due to account lockout after repeated failed attempts.",
                cancellationToken: cancellationToken);

            throw new ForbiddenAppException(
                "Account is temporarily locked due to repeated failed login attempts.",
                "account_locked");
        }

        if (check.Status == CredentialCheckStatus.EmailNotVerified)
        {
            throw new ForbiddenAppException(
                "Please verify your email address before signing in.",
                "email_not_verified");
        }

        if (check.Status == CredentialCheckStatus.InactiveUser)
        {
            throw new ForbiddenAppException(
                "This user account has been deactivated.",
                "account_inactive");
        }

        if (check.Status != CredentialCheckStatus.Succeeded || check.User is null)
        {
            await _auditLogger.LogPlatformAsync(
                null,
                normalizedEmail,
                AuditCategory.Security,
                "LOGIN_FAILED",
                "User",
                normalizedEmail,
                "Invalid login credentials provided.",
                cancellationToken: cancellationToken);

            throw new UnauthorizedAppException("Invalid email or password.", "invalid_credentials");
        }

        var user = check.User;
        var now = _dateTimeProvider.UtcNow;

        var session = new UserSession(
            user.Id,
            _requestContextInfo.IpAddress,
            _requestContextInfo.UserAgent,
            now);

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, session.Id);
        var refreshTokenPair = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshToken = new RefreshToken(
            user.Id,
            session.Id,
            Guid.CreateVersion7(),
            refreshTokenPair.TokenHash,
            now,
            refreshTokenPair.ExpiresAtUtc);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _sessionStore.AddSessionAsync(session, ct);
            await _sessionStore.AddRefreshTokenAsync(refreshToken, ct);
            await _identityService.UpdateLastLoginAsync(user.Id, now, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _auditLogger.LogPlatformAsync(
            user.Id,
            user.Email,
            AuditCategory.Identity,
            "USER_LOGIN",
            "UserSession",
            session.Id.ToString(),
            "User signed in successfully.",
            cancellationToken: cancellationToken);

        var tenants = await _tenantStore.ListUserTenantsAsync(user.Id, cancellationToken);

        return new AuthTokenResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshTokenPair.RawToken,
            refreshTokenPair.ExpiresAtUtc,
            new AuthenticatedUserDto(user.Id, user.Email, user.FullName, user.PlatformRole),
            tenants);
    }
}
