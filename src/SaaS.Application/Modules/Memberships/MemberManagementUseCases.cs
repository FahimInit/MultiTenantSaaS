using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.Roles;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Audit;
using SaaS.Domain.Memberships;

namespace SaaS.Application.Modules.Memberships;

public sealed record ListMembersQuery(
    int Page = 1,
    int PageSize = PaginationQuery.DefaultPageSize,
    MembershipStatus? Status = null,
    Guid? RoleId = null,
    string? Search = null,
    string? SortBy = null,
    string SortDirection = "desc") : PaginationQuery, IQuery<PagedResult<MemberListItemDto>>
{
    public new int Page { get; init; } = Page;
    public new int PageSize { get; init; } = PageSize;
    public new string? Search { get; init; } = Search;
    public new string? SortBy { get; init; } = SortBy;
    public new string SortDirection { get; init; } = SortDirection;
}

public sealed record UpdateMemberRoleCommand(Guid MemberId, Guid RoleId) : ICommand<MemberListItemDto>;

public sealed record UpdateMemberStatusCommand(Guid MemberId, MembershipStatus Status) : ICommand<MemberListItemDto>;

public sealed record RemoveMemberCommand(Guid MemberId) : ICommand;

public sealed class UpdateMemberRoleValidator : AbstractValidator<UpdateMemberRoleCommand>
{
    public UpdateMemberRoleValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class UpdateMemberStatusValidator : AbstractValidator<UpdateMemberStatusCommand>
{
    public UpdateMemberStatusValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Status)
            .Must(s => s is MembershipStatus.Active or MembershipStatus.Suspended)
            .WithMessage("Member status can only be set to Active or Suspended; use DELETE to remove a member.");
    }
}

public sealed class ListMembersHandler : IQueryHandler<ListMembersQuery, PagedResult<MemberListItemDto>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IMembershipStore _membershipStore;

    public ListMembersHandler(ITenantContext tenantContext, IMembershipStore membershipStore)
    {
        _tenantContext = tenantContext;
        _membershipStore = membershipStore;
    }

    public Task<PagedResult<MemberListItemDto>> HandleAsync(
        ListMembersQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();

        return _membershipStore.ListMembersAsync(
            tenantId,
            new MemberFilterQuery(
                query.NormalizedPage,
                query.NormalizedPageSize,
                query.Search,
                query.Status,
                query.RoleId,
                query.SortBy,
                query.IsAscending),
            cancellationToken);
    }
}

public sealed class UpdateMemberRoleHandler : ICommandHandler<UpdateMemberRoleCommand, MemberListItemDto>
{
    private readonly IValidator<UpdateMemberRoleCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStore _membershipStore;
    private readonly IRoleStore _roleStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly ITenantPermissionCacheInvalidator _cacheInvalidator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public UpdateMemberRoleHandler(
        IValidator<UpdateMemberRoleCommand> validator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IMembershipStore membershipStore,
        IRoleStore roleStore,
        ISubscriptionQuotaService quotaService,
        ITenantPermissionCacheInvalidator cacheInvalidator,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _membershipStore = membershipStore;
        _roleStore = roleStore;
        _quotaService = quotaService;
        _cacheInvalidator = cacheInvalidator;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<MemberListItemDto> HandleAsync(
        UpdateMemberRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        await _quotaService.EnsureWorkspaceWriteAllowedAsync(tenantId, cancellationToken);

        var target = await _membershipStore.FindMemberDetailAsync(tenantId, command.MemberId, cancellationToken)
            ?? throw new NotFoundAppException("Organization member was not found.", "member_not_found");

        if (target.Membership.Status == MembershipStatus.Removed)
        {
            throw new ConflictAppException("Cannot update the role of a removed member.", "member_removed");
        }

        PermissionHierarchyGuard.EnsureCallerCanModifyMemberWithCurrentRole(_tenantContext, target.RoleCode);

        var targetRole = await _roleStore.FindByIdWithPermissionsAsync(tenantId, command.RoleId, cancellationToken)
            ?? throw new NotFoundAppException("Target role was not found in this organization.", "role_not_found");

        PermissionHierarchyGuard.EnsureCallerCanAssignRole(_tenantContext, targetRole);

        if (target.RoleCode == SystemRoleCodes.OrganizationOwner &&
            targetRole.Code != SystemRoleCodes.OrganizationOwner &&
            target.Membership.Status == MembershipStatus.Active)
        {
            var otherOwners = await _membershipStore.CountActiveOwnersAsync(
                tenantId,
                excludeMembershipId: target.Membership.Id,
                cancellationToken);

            if (otherOwners == 0)
            {
                throw new ConflictAppException(
                    "Cannot demote the last active Organization Owner. Assign another owner first.",
                    "last_owner_protected");
            }
        }

        var previousRoleCode = target.RoleCode;
        target.Membership.ChangeRole(targetRole.Id, _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateUserTenantAsync(tenantId, target.Membership.UserId, cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.Membership,
            "MEMBER_ROLE_UPDATED",
            "TenantMembership",
            target.Membership.Id.ToString(),
            $"Changed role of '{target.UserEmail}' from {previousRoleCode} to {targetRole.Code}.",
            new Dictionary<string, object?>
            {
                ["targetUserId"] = target.Membership.UserId,
                ["previousRoleCode"] = previousRoleCode,
                ["newRoleCode"] = targetRole.Code
            },
            cancellationToken);

        return new MemberListItemDto(
            target.Membership.Id,
            target.Membership.UserId,
            target.UserEmail,
            target.UserFullName,
            targetRole.Id,
            targetRole.Code,
            targetRole.Name,
            targetRole.IsSystemRole,
            target.Membership.Status,
            target.Membership.JoinedAtUtc,
            null);
    }
}

public sealed class UpdateMemberStatusHandler : ICommandHandler<UpdateMemberStatusCommand, MemberListItemDto>
{
    private readonly IValidator<UpdateMemberStatusCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStore _membershipStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly ITenantPermissionCacheInvalidator _cacheInvalidator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public UpdateMemberStatusHandler(
        IValidator<UpdateMemberStatusCommand> validator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IMembershipStore membershipStore,
        ISubscriptionQuotaService quotaService,
        ITenantPermissionCacheInvalidator cacheInvalidator,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _membershipStore = membershipStore;
        _quotaService = quotaService;
        _cacheInvalidator = cacheInvalidator;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<MemberListItemDto> HandleAsync(
        UpdateMemberStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        var target = await _membershipStore.FindMemberDetailAsync(tenantId, command.MemberId, cancellationToken)
            ?? throw new NotFoundAppException("Organization member was not found.", "member_not_found");

        if (target.Membership.Status == MembershipStatus.Removed)
        {
            throw new ConflictAppException("Cannot change status of a removed member.", "member_removed");
        }

        if (command.Status == MembershipStatus.Active && target.Membership.Status != MembershipStatus.Active)
        {
            await _quotaService.EnsureCanAddMemberOrInviteAsync(tenantId, cancellationToken);
        }
        else
        {
            await _quotaService.EnsureWorkspaceWriteAllowedAsync(tenantId, cancellationToken);
        }

        PermissionHierarchyGuard.EnsureCallerCanModifyMemberWithCurrentRole(_tenantContext, target.RoleCode);

        var now = _dateTimeProvider.UtcNow;
        if (command.Status == MembershipStatus.Suspended)
        {
            if (target.Membership.UserId == actorId)
            {
                throw new ConflictAppException("You cannot suspend your own membership.", "cannot_suspend_self");
            }

            if (target.RoleCode == SystemRoleCodes.OrganizationOwner && target.Membership.Status == MembershipStatus.Active)
            {
                var otherOwners = await _membershipStore.CountActiveOwnersAsync(
                    tenantId,
                    excludeMembershipId: target.Membership.Id,
                    cancellationToken);
                if (otherOwners == 0)
                {
                    throw new ConflictAppException(
                        "Cannot suspend the last active Organization Owner.",
                        "last_owner_protected");
                }
            }

            target.Membership.Suspend(now);
        }
        else
        {
            target.Membership.Activate(now);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _quotaService.SyncUsageCountsAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _cacheInvalidator.InvalidateUserTenantAsync(tenantId, target.Membership.UserId, cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.Membership,
            command.Status == MembershipStatus.Suspended ? "MEMBER_SUSPENDED" : "MEMBER_ACTIVATED",
            "TenantMembership",
            target.Membership.Id.ToString(),
            $"Updated member '{target.UserEmail}' status to {command.Status}.",
            cancellationToken: cancellationToken);

        return new MemberListItemDto(
            target.Membership.Id,
            target.Membership.UserId,
            target.UserEmail,
            target.UserFullName,
            target.Membership.RoleId,
            target.RoleCode,
            target.RoleName,
            false,
            target.Membership.Status,
            target.Membership.JoinedAtUtc,
            null);
    }
}

public sealed class RemoveMemberHandler : ICommandHandler<RemoveMemberCommand>
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStore _membershipStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly ITenantPermissionCacheInvalidator _cacheInvalidator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public RemoveMemberHandler(
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IMembershipStore membershipStore,
        ISubscriptionQuotaService quotaService,
        ITenantPermissionCacheInvalidator cacheInvalidator,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _membershipStore = membershipStore;
        _quotaService = quotaService;
        _cacheInvalidator = cacheInvalidator;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(RemoveMemberCommand command, CancellationToken cancellationToken = default)
    {
        if (command.MemberId == Guid.Empty)
        {
            throw new NotFoundAppException("Organization member was not found.", "member_not_found");
        }

        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        await _quotaService.EnsureWorkspaceWriteAllowedAsync(tenantId, cancellationToken);

        var target = await _membershipStore.FindMemberDetailAsync(tenantId, command.MemberId, cancellationToken)
            ?? throw new NotFoundAppException("Organization member was not found.", "member_not_found");

        if (target.Membership.Status == MembershipStatus.Removed)
        {
            return;
        }

        PermissionHierarchyGuard.EnsureCallerCanModifyMemberWithCurrentRole(_tenantContext, target.RoleCode);

        if (target.RoleCode == SystemRoleCodes.OrganizationOwner && target.Membership.Status == MembershipStatus.Active)
        {
            var otherOwners = await _membershipStore.CountActiveOwnersAsync(
                tenantId,
                excludeMembershipId: target.Membership.Id,
                cancellationToken);
            if (otherOwners == 0)
            {
                throw new ConflictAppException(
                    "Cannot remove the last active Organization Owner. Transfer ownership first.",
                    "last_owner_protected");
            }
        }

        target.Membership.Remove(_dateTimeProvider.UtcNow);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _quotaService.SyncUsageCountsAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _cacheInvalidator.InvalidateUserTenantAsync(tenantId, target.Membership.UserId, cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.Membership,
            "MEMBER_REMOVED",
            "TenantMembership",
            target.Membership.Id.ToString(),
            $"Removed member '{target.UserEmail}' from the organization.",
            new Dictionary<string, object?> { ["targetUserId"] = target.Membership.UserId },
            cancellationToken);
    }
}
