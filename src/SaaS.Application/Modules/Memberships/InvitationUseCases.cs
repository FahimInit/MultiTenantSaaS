using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.Roles;
using SaaS.Domain.Audit;
using SaaS.Domain.Invitations;
using SaaS.Domain.Memberships;

namespace SaaS.Application.Modules.Memberships;

public sealed record AcceptInvitationResponse(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string RoleCode,
    string RoleName);

public sealed record ListInvitationsQuery(
    int Page = 1,
    int PageSize = PaginationQuery.DefaultPageSize,
    InvitationStatus? Status = null,
    string? Search = null,
    string SortDirection = "desc") : PaginationQuery, IQuery<PagedResult<InvitationListItemDto>>
{
    public new int Page { get; init; } = Page;
    public new int PageSize { get; init; } = PageSize;
    public new string? Search { get; init; } = Search;
    public new string SortDirection { get; init; } = SortDirection;
}

public sealed record InviteMemberCommand(string Email, Guid RoleId) : ICommand<InvitationListItemDto>;
public sealed record ResendInvitationCommand(Guid InvitationId) : ICommand<InvitationListItemDto>;
public sealed record RevokeInvitationCommand(Guid InvitationId) : ICommand;
public sealed record AcceptInvitationCommand(string Token) : ICommand<AcceptInvitationResponse>;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
    }
}

public sealed class ListInvitationsHandler
    : IQueryHandler<ListInvitationsQuery, PagedResult<InvitationListItemDto>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IInvitationStore _invitationStore;

    public ListInvitationsHandler(ITenantContext tenantContext, IInvitationStore invitationStore)
    {
        _tenantContext = tenantContext;
        _invitationStore = invitationStore;
    }

    public Task<PagedResult<InvitationListItemDto>> HandleAsync(
        ListInvitationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();

        return _invitationStore.ListInvitationsAsync(
            tenantId,
            new InvitationFilterQuery(
                query.NormalizedPage,
                query.NormalizedPageSize,
                query.Status,
                query.Search,
                query.IsAscending),
            cancellationToken);
    }
}

public sealed class InviteMemberHandler : ICommandHandler<InviteMemberCommand, InvitationListItemDto>
{
    private readonly IValidator<InviteMemberCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantStore _tenantStore;
    private readonly IRoleStore _roleStore;
    private readonly IMembershipStore _membershipStore;
    private readonly IInvitationStore _invitationStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly IOpaqueTokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public InviteMemberHandler(
        IValidator<InviteMemberCommand> validator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ITenantStore tenantStore,
        IRoleStore roleStore,
        IMembershipStore membershipStore,
        IInvitationStore invitationStore,
        ISubscriptionQuotaService quotaService,
        IOpaqueTokenService tokenService,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _tenantStore = tenantStore;
        _roleStore = roleStore;
        _membershipStore = membershipStore;
        _invitationStore = invitationStore;
        _quotaService = quotaService;
        _tokenService = tokenService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<InvitationListItemDto> HandleAsync(
        InviteMemberCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        await _quotaService.EnsureCanAddMemberOrInviteAsync(tenantId, cancellationToken);

        var tenant = await _tenantStore.GetCurrentTenantAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization was not found.", "tenant_not_found");

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var settings = await _tenantStore.GetCurrentTenantSettingsAsync(tenantId, cancellationToken);
        if (settings is { EnforceEmailDomain: true } && settings.AllowedEmailDomains.Count > 0)
        {
            var domain = normalizedEmail.Split('@').LastOrDefault() ?? string.Empty;
            if (!settings.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            {
                throw new ValidationAppException(
                    new Dictionary<string, string[]>
                    {
                        ["email"] = [$"Only email addresses from allowed organization domains ({string.Join(", ", settings.AllowedEmailDomains)}) may be invited."]
                    },
                    "Email domain is restricted by organization security policy.");
            }
        }

        var role = await _roleStore.FindByIdWithPermissionsAsync(tenantId, command.RoleId, cancellationToken)
            ?? throw new NotFoundAppException("Target role was not found in this organization.", "role_not_found");

        PermissionHierarchyGuard.EnsureCallerCanAssignRole(_tenantContext, role);

        if (await _membershipStore.HasActiveOrSuspendedMemberByEmailAsync(tenantId, normalizedEmail, cancellationToken))
        {
            throw new ConflictAppException(
                $"'{normalizedEmail}' is already a member of this organization.",
                "member_already_exists");
        }

        var existingPending = await _invitationStore.FindPendingByEmailAsync(tenantId, normalizedEmail, cancellationToken);
        if (existingPending is not null)
        {
            throw new ConflictAppException(
                $"A pending invitation already exists for '{normalizedEmail}'.",
                "invitation_already_pending");
        }

        var now = _dateTimeProvider.UtcNow;
        var expiresAt = now.AddDays(7);
        var (rawToken, tokenHash) = _tokenService.GenerateTokenPair();

        var invitation = new TenantInvitation(
            tenantId,
            command.Email,
            role.Id,
            tokenHash,
            actorId,
            expiresAt,
            now);

        await _invitationStore.AddInvitationAsync(invitation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendTenantInvitationAsync(
            invitation.Email,
            tenant.Name,
            role.Name,
            rawToken,
            cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.Membership,
            "MEMBER_INVITED",
            "TenantInvitation",
            invitation.Id.ToString(),
            $"Invited '{invitation.Email}' with role '{role.Name}'.",
            new Dictionary<string, object?> { ["roleCode"] = role.Code },
            cancellationToken);

        return new InvitationListItemDto(
            invitation.Id,
            invitation.Email,
            role.Id,
            role.Code,
            role.Name,
            invitation.Status,
            actorEmail,
            invitation.ExpiresAtUtc,
            invitation.CreatedAtUtc,
            invitation.AcceptedAtUtc);
    }
}

public sealed class ResendInvitationHandler : ICommandHandler<ResendInvitationCommand, InvitationListItemDto>
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantStore _tenantStore;
    private readonly IRoleStore _roleStore;
    private readonly IInvitationStore _invitationStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly IOpaqueTokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public ResendInvitationHandler(
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ITenantStore tenantStore,
        IRoleStore roleStore,
        IInvitationStore invitationStore,
        ISubscriptionQuotaService quotaService,
        IOpaqueTokenService tokenService,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _tenantStore = tenantStore;
        _roleStore = roleStore;
        _invitationStore = invitationStore;
        _quotaService = quotaService;
        _tokenService = tokenService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<InvitationListItemDto> HandleAsync(
        ResendInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        await _quotaService.EnsureWorkspaceWriteAllowedAsync(tenantId, cancellationToken);

        var invitation = await _invitationStore.FindByIdAsync(tenantId, command.InvitationId, cancellationToken)
            ?? throw new NotFoundAppException("Invitation was not found.", "invitation_not_found");

        var tenant = await _tenantStore.GetCurrentTenantAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization was not found.", "tenant_not_found");

        var role = await _roleStore.FindByIdWithPermissionsAsync(tenantId, invitation.RoleId, cancellationToken)
            ?? throw new NotFoundAppException("Assigned invitation role no longer exists.", "role_not_found");

        var now = _dateTimeProvider.UtcNow;
        var (rawToken, tokenHash) = _tokenService.GenerateTokenPair();
        invitation.RefreshForResend(tokenHash, now.AddDays(7), now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendTenantInvitationAsync(
            invitation.Email,
            tenant.Name,
            role.Name,
            rawToken,
            cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.Membership,
            "INVITATION_RESENT",
            "TenantInvitation",
            invitation.Id.ToString(),
            $"Resent invitation to '{invitation.Email}'.",
            cancellationToken: cancellationToken);

        return new InvitationListItemDto(
            invitation.Id,
            invitation.Email,
            role.Id,
            role.Code,
            role.Name,
            invitation.Status,
            actorEmail,
            invitation.ExpiresAtUtc,
            invitation.CreatedAtUtc,
            invitation.AcceptedAtUtc);
    }
}

public sealed class RevokeInvitationHandler : ICommandHandler<RevokeInvitationCommand>
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IInvitationStore _invitationStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public RevokeInvitationHandler(
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IInvitationStore invitationStore,
        ISubscriptionQuotaService quotaService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _invitationStore = invitationStore;
        _quotaService = quotaService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(RevokeInvitationCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        await _quotaService.EnsureWorkspaceWriteAllowedAsync(tenantId, cancellationToken);

        var invitation = await _invitationStore.FindByIdAsync(tenantId, command.InvitationId, cancellationToken)
            ?? throw new NotFoundAppException("Invitation was not found.", "invitation_not_found");

        invitation.Revoke(_dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.Membership,
            "INVITATION_REVOKED",
            "TenantInvitation",
            invitation.Id.ToString(),
            $"Revoked invitation for '{invitation.Email}'.",
            cancellationToken: cancellationToken);
    }
}

public sealed class AcceptInvitationHandler : ICommandHandler<AcceptInvitationCommand, AcceptInvitationResponse>
{
    private readonly IValidator<AcceptInvitationCommand> _validator;
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;
    private readonly IOpaqueTokenService _tokenService;
    private readonly IInvitationStore _invitationStore;
    private readonly IMembershipStore _membershipStore;
    private readonly ITenantStore _tenantStore;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public AcceptInvitationHandler(
        IValidator<AcceptInvitationCommand> validator,
        ICurrentUser currentUser,
        IIdentityService identityService,
        IOpaqueTokenService tokenService,
        IInvitationStore invitationStore,
        IMembershipStore membershipStore,
        ITenantStore tenantStore,
        ISubscriptionStore subscriptionStore,
        ISubscriptionQuotaService quotaService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _currentUser = currentUser;
        _identityService = identityService;
        _tokenService = tokenService;
        _invitationStore = invitationStore;
        _membershipStore = membershipStore;
        _tenantStore = tenantStore;
        _subscriptionStore = subscriptionStore;
        _quotaService = quotaService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<AcceptInvitationResponse> HandleAsync(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var user = await _identityService.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAppException();

        if (!user.EmailConfirmed || !user.IsActive)
        {
            throw new ForbiddenAppException(
                "Please verify your email address before accepting an organization invitation.",
                "email_not_verified");
        }

        var tokenHash = _tokenService.HashToken(command.Token.Trim());
        var lookup = await _invitationStore.FindForAcceptanceByHashAsync(tokenHash, cancellationToken)
            ?? throw new NotFoundAppException("Invitation token is invalid or no longer exists.", "invalid_invitation_token");

        if (!lookup.TenantIsOperational)
        {
            throw new ForbiddenAppException(
                $"The organization '{lookup.TenantName}' is not currently accepting members.",
                "tenant_unavailable");
        }

        var now = _dateTimeProvider.UtcNow;
        var invitation = lookup.Invitation;

        var subscriptionWithPlan = await _subscriptionStore.GetTenantSubscriptionWithPlanUnfilteredAsync(
            invitation.TenantId,
            cancellationToken);
        if (subscriptionWithPlan is not null)
        {
            if (!subscriptionWithPlan.Subscription.AllowsWorkspaceWritesAt(now))
            {
                throw new QuotaExceededAppException(
                    "The target organization's subscription is inactive or expired.",
                    "subscription_inactive_or_expired");
            }

            if (subscriptionWithPlan.Plan.MaxMembers.HasValue)
            {
                var activeMembers = await _membershipStore.CountActiveMembersUnfilteredAsync(
                    invitation.TenantId,
                    cancellationToken);
                if (activeMembers >= subscriptionWithPlan.Plan.MaxMembers.Value)
                {
                    throw new QuotaExceededAppException(
                        "The organization has reached its maximum active member limit.",
                        "member_quota_exceeded");
                }
            }
        }

        invitation.Accept(user.Id, user.Email, now);

        var existingMembership = await _membershipStore.FindByTenantAndUserUnfilteredAsync(
            invitation.TenantId,
            user.Id,
            cancellationToken);

        if (existingMembership is not null && existingMembership.Status != MembershipStatus.Removed)
        {
            throw new ConflictAppException(
                "You already hold a membership in this organization.",
                "member_already_exists");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (existingMembership is not null)
            {
                existingMembership.Rejoin(invitation.RoleId, invitation.InvitedByUserId, now);
            }
            else
            {
                var membership = new TenantMembership(
                    invitation.TenantId,
                    user.Id,
                    invitation.RoleId,
                    now,
                    invitation.InvitedByUserId);
                await _tenantStore.AddMembershipAsync(membership, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _quotaService.SyncUsageCountsUnfilteredAsync(invitation.TenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _auditLogger.LogTenantAsync(
            invitation.TenantId,
            user.Id,
            user.Email,
            AuditCategory.Membership,
            "INVITATION_ACCEPTED",
            "TenantInvitation",
            invitation.Id.ToString(),
            $"'{user.Email}' accepted invitation and joined '{lookup.TenantName}' as {lookup.RoleName}.",
            new Dictionary<string, object?> { ["roleCode"] = lookup.RoleCode },
            cancellationToken);

        return new AcceptInvitationResponse(
            invitation.TenantId,
            lookup.TenantName,
            lookup.TenantSlug,
            lookup.RoleCode,
            lookup.RoleName);
    }
}
