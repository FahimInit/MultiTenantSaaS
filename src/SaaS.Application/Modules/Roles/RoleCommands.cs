using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Audit;

namespace SaaS.Application.Modules.Roles;

public sealed record CreateCustomRoleCommand(
    string Name,
    string Description,
    IReadOnlyList<string> Permissions) : ICommand<TenantRoleSummaryDto>;

public sealed record UpdateRoleCommand(
    Guid RoleId,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions) : ICommand<TenantRoleSummaryDto>;

public sealed record DeleteRoleCommand(Guid RoleId) : ICommand;

public sealed class CreateCustomRoleValidator : AbstractValidator<CreateCustomRoleCommand>
{
    public CreateCustomRoleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(400);
        RuleFor(x => x.Permissions).NotEmpty().WithMessage("At least one permission must be selected.");
        RuleForEach(x => x.Permissions)
            .Must(PermissionCatalog.IsValidTenantPermission)
            .WithMessage(p => $"'{p}' is not a recognized tenant permission.");
    }
}

public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(400);
        RuleFor(x => x.Permissions).NotEmpty().WithMessage("At least one permission must be selected.");
        RuleForEach(x => x.Permissions)
            .Must(PermissionCatalog.IsValidTenantPermission)
            .WithMessage(p => $"'{p}' is not a recognized tenant permission.");
    }
}

public sealed class CreateCustomRoleHandler : ICommandHandler<CreateCustomRoleCommand, TenantRoleSummaryDto>
{
    private readonly IValidator<CreateCustomRoleCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IRoleStore _roleStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public CreateCustomRoleHandler(
        IValidator<CreateCustomRoleCommand> validator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IRoleStore roleStore,
        ISubscriptionQuotaService quotaService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _roleStore = roleStore;
        _quotaService = quotaService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<TenantRoleSummaryDto> HandleAsync(
        CreateCustomRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        await _quotaService.EnsureCanCreateCustomRoleAsync(tenantId, cancellationToken);
        PermissionHierarchyGuard.EnsureCallerCanGrantPermissions(_tenantContext, command.Permissions);

        var trimmedName = command.Name.Trim();
        if (await _roleStore.RoleNameExistsAsync(tenantId, trimmedName, null, cancellationToken))
        {
            throw new ConflictAppException(
                $"A role named '{trimmedName}' already exists in this organization.",
                "duplicate_role_name");
        }

        var now = _dateTimeProvider.UtcNow;
        var role = TenantRole.CreateCustom(tenantId, trimmedName, command.Description, command.Permissions, now);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _roleStore.AddRoleAsync(role, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _quotaService.SyncUsageCountsAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.AccessControl,
            "ROLE_CREATED",
            "TenantRole",
            role.Id.ToString(),
            $"Created custom role '{role.Name}'.",
            new Dictionary<string, object?>
            {
                ["roleCode"] = role.Code,
                ["permissionCount"] = role.Permissions.Count
            },
            cancellationToken);

        return new TenantRoleSummaryDto(
            role.Id,
            role.Code,
            role.Name,
            role.Description,
            role.IsSystemRole,
            0,
            role.Permissions.Select(p => p.PermissionCode).OrderBy(p => p).ToList(),
            role.CreatedAtUtc,
            role.UpdatedAtUtc);
    }
}

public sealed class UpdateRoleHandler : ICommandHandler<UpdateRoleCommand, TenantRoleSummaryDto>
{
    private readonly IValidator<UpdateRoleCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IRoleStore _roleStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public UpdateRoleHandler(
        IValidator<UpdateRoleCommand> validator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IRoleStore roleStore,
        ISubscriptionQuotaService quotaService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _roleStore = roleStore;
        _quotaService = quotaService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<TenantRoleSummaryDto> HandleAsync(
        UpdateRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        await _quotaService.EnsureWorkspaceWriteAllowedAsync(tenantId, cancellationToken);

        var role = await _roleStore.FindByIdWithPermissionsAsync(tenantId, command.RoleId, cancellationToken)
            ?? throw new NotFoundAppException("Role was not found.", "role_not_found");

        PermissionHierarchyGuard.EnsureCallerCanGrantPermissions(_tenantContext, command.Permissions);

        var trimmedName = command.Name.Trim();
        if (!role.IsSystemRole && await _roleStore.RoleNameExistsAsync(tenantId, trimmedName, role.Id, cancellationToken))
        {
            throw new ConflictAppException(
                $"A role named '{trimmedName}' already exists in this organization.",
                "duplicate_role_name");
        }

        role.UpdateDetails(trimmedName, command.Description, command.Permissions, _dateTimeProvider.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var memberCount = await _roleStore.CountRoleReferencesAsync(tenantId, role.Id, cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.AccessControl,
            "ROLE_UPDATED",
            "TenantRole",
            role.Id.ToString(),
            $"Updated permissions for role '{role.Name}'.",
            new Dictionary<string, object?>
            {
                ["roleCode"] = role.Code,
                ["permissionCount"] = role.Permissions.Count
            },
            cancellationToken);

        return new TenantRoleSummaryDto(
            role.Id,
            role.Code,
            role.Name,
            role.Description,
            role.IsSystemRole,
            memberCount,
            role.Permissions.Select(p => p.PermissionCode).OrderBy(p => p).ToList(),
            role.CreatedAtUtc,
            role.UpdatedAtUtc);
    }
}

public sealed class DeleteRoleHandler : ICommandHandler<DeleteRoleCommand>
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IRoleStore _roleStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public DeleteRoleHandler(
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IRoleStore roleStore,
        ISubscriptionQuotaService quotaService,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _roleStore = roleStore;
        _quotaService = quotaService;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(DeleteRoleCommand command, CancellationToken cancellationToken = default)
    {
        if (command.RoleId == Guid.Empty)
        {
            throw new NotFoundAppException("Role was not found.", "role_not_found");
        }

        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        await _quotaService.EnsureWorkspaceWriteAllowedAsync(tenantId, cancellationToken);

        var role = await _roleStore.FindByIdWithPermissionsAsync(tenantId, command.RoleId, cancellationToken)
            ?? throw new NotFoundAppException("Role was not found.", "role_not_found");

        if (role.IsSystemRole)
        {
            throw new ConflictAppException("Built-in system roles cannot be deleted.", "cannot_delete_system_role");
        }

        var references = await _roleStore.CountRoleReferencesAsync(tenantId, role.Id, cancellationToken);
        if (references > 0)
        {
            throw new ConflictAppException(
                "Cannot delete a role while members or pending invitations are assigned to it.",
                "role_in_use");
        }

        var roleName = role.Name;
        var roleId = role.Id;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            _roleStore.RemoveRole(role);
            await _unitOfWork.SaveChangesAsync(ct);
            await _quotaService.SyncUsageCountsAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.AccessControl,
            "ROLE_DELETED",
            "TenantRole",
            roleId.ToString(),
            $"Deleted custom role '{roleName}'.",
            cancellationToken: cancellationToken);
    }
}
