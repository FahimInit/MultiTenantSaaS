using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;

namespace SaaS.Application.Modules.Identity;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;

public sealed class GetCurrentUserHandler : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;
    private readonly ITenantStore _tenantStore;

    public GetCurrentUserHandler(
        ICurrentUser currentUser,
        IIdentityService identityService,
        ITenantStore tenantStore)
    {
        _currentUser = currentUser;
        _identityService = identityService;
        _tenantStore = tenantStore;
    }

    public async Task<CurrentUserResponse> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var user = await _identityService.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAppException();

        var tenants = await _tenantStore.ListUserTenantsAsync(userId, cancellationToken);

        return new CurrentUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.EmailConfirmed,
            user.PlatformRole,
            tenants);
    }
}

public sealed record GetActiveSessionsQuery : IQuery<IReadOnlyList<UserSessionDto>>;

public sealed class GetActiveSessionsHandler : IQueryHandler<GetActiveSessionsQuery, IReadOnlyList<UserSessionDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISessionStore _sessionStore;

    public GetActiveSessionsHandler(ICurrentUser currentUser, ISessionStore sessionStore)
    {
        _currentUser = currentUser;
        _sessionStore = sessionStore;
    }

    public async Task<IReadOnlyList<UserSessionDto>> HandleAsync(
        GetActiveSessionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var sessions = await _sessionStore.ListActiveSessionsAsync(userId, cancellationToken);

        return sessions
            .Select(s => new UserSessionDto(s.Id, s.IpAddress, s.UserAgent, s.CreatedAtUtc, s.LastSeenAtUtc, s.IsActive))
            .ToList();
    }
}

public sealed record RevokeSessionCommand(Guid SessionId) : ICommand;

public sealed class RevokeSessionHandler : ICommandHandler<RevokeSessionCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISessionStore _sessionStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public RevokeSessionHandler(
        ICurrentUser currentUser,
        ISessionStore sessionStore,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _currentUser = currentUser;
        _sessionStore = sessionStore;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(RevokeSessionCommand command, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var session = await _sessionStore.FindSessionByIdAsync(command.SessionId, cancellationToken);

        // Enforce IDOR protection: do not reveal existence of another user's session.
        if (session is null || session.UserId != userId)
        {
            throw new NotFoundAppException("Session was not found.", "session_not_found");
        }

        if (!session.IsActive)
        {
            return;
        }

        var now = _dateTimeProvider.UtcNow;
        await _sessionStore.RevokeSessionAndTokensAsync(
            session.Id,
            "Session revoked by user",
            now,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogPlatformAsync(
            userId,
            _currentUser.Email ?? "user",
            AuditCategory.Identity,
            "SESSION_REVOKED",
            "UserSession",
            session.Id.ToString(),
            "User manually revoked an active session.",
            cancellationToken: cancellationToken);
    }
}
