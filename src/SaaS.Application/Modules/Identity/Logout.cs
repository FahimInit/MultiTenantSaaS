using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;

namespace SaaS.Application.Modules.Identity;

public sealed record LogoutCommand(string RefreshToken) : ICommand;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class LogoutHandler : ICommandHandler<LogoutCommand>
{
    private readonly IValidator<LogoutCommand> _validator;
    private readonly ISessionStore _sessionStore;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public LogoutHandler(
        IValidator<LogoutCommand> validator,
        ISessionStore sessionStore,
        IJwtTokenGenerator jwtTokenGenerator,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _sessionStore = sessionStore;
        _jwtTokenGenerator = jwtTokenGenerator;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var hash = _jwtTokenGenerator.HashRefreshToken(command.RefreshToken.Trim());
        var token = await _sessionStore.FindRefreshTokenByHashAsync(hash, cancellationToken);
        if (token is null)
        {
            return;
        }

        if (_currentUser.IsAuthenticated && _currentUser.UserId.HasValue && token.UserId != _currentUser.UserId.Value)
        {
            throw new ForbiddenAppException("Cannot revoke a token belonging to another user.", "forbidden");
        }

        var now = _dateTimeProvider.UtcNow;
        await _sessionStore.RevokeTokenFamilyAndSessionAsync(
            token.TokenFamilyId,
            token.SessionId,
            "User logout",
            now,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogPlatformAsync(
            token.UserId,
            _currentUser.Email ?? "user",
            AuditCategory.Identity,
            "USER_LOGOUT",
            "UserSession",
            token.SessionId.ToString(),
            "User logged out and revoked active session.",
            cancellationToken: cancellationToken);
    }
}
