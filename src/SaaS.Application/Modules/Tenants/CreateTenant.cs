using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Audit;
using SaaS.Domain.Memberships;
using SaaS.Domain.Tenants;

namespace SaaS.Application.Modules.Tenants;

public sealed record CreateTenantCommand(string Name, string Slug) : ICommand<TenantDetailDto>;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Slug).NotEmpty().MinimumLength(3).MaximumLength(63);
    }
}

public sealed class CreateTenantHandler : ICommandHandler<CreateTenantCommand, TenantDetailDto>
{
    private readonly IValidator<CreateTenantCommand> _validator;
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;
    private readonly ITenantStore _tenantStore;
    private readonly ISubscriptionProvisioner _subscriptionProvisioner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public CreateTenantHandler(
        IValidator<CreateTenantCommand> validator,
        ICurrentUser currentUser,
        IIdentityService identityService,
        ITenantStore tenantStore,
        ISubscriptionProvisioner subscriptionProvisioner,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _currentUser = currentUser;
        _identityService = identityService;
        _tenantStore = tenantStore;
        _subscriptionProvisioner = subscriptionProvisioner;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<TenantDetailDto> HandleAsync(
        CreateTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var user = await _identityService.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAppException();

        if (!user.EmailConfirmed || !user.IsActive)
        {
            throw new ForbiddenAppException(
                "A verified active user account is required to create an organization.",
                "email_not_verified");
        }

        var now = _dateTimeProvider.UtcNow;
        var tenant = Tenant.Create(command.Name, command.Slug, user.Id, now);

        if (await _tenantStore.SlugExistsAsync(tenant.Slug.Value, cancellationToken: cancellationToken))
        {
            throw new ConflictAppException(
                $"An organization with slug '{tenant.Slug.Value}' already exists.",
                "duplicate_tenant_slug");
        }

        var settings = new TenantSettings(tenant.Id, user.Email, now);
        var roles = TenantRole.CreateSystemRolesForTenant(tenant.Id, now);
        var ownerRole = roles.First(r => r.Code == SystemRoleCodes.OrganizationOwner);
        var ownerMembership = new TenantMembership(tenant.Id, user.Id, ownerRole.Id, now);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _tenantStore.AddTenantAsync(tenant, ct);
            await _tenantStore.AddTenantSettingsAsync(settings, ct);
            await _tenantStore.AddTenantRolesAsync(roles, ct);
            await _tenantStore.AddMembershipAsync(ownerMembership, ct);
            await _subscriptionProvisioner.ProvisionInitialSubscriptionAsync(tenant.Id, user.Id, now, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenant.Id,
            user.Id,
            user.Email,
            AuditCategory.Tenant,
            "TENANT_CREATED",
            "Tenant",
            tenant.Id.ToString(),
            $"Organization '{tenant.Name}' created.",
            new Dictionary<string, object?> { ["slug"] = tenant.Slug.Value },
            cancellationToken);

        return new TenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug.Value,
            tenant.Status,
            tenant.IsOnboardingCompleted,
            tenant.OnboardingCompletedAtUtc,
            tenant.CreatedAtUtc,
            ownerRole.Code,
            ownerRole.Permissions.Select(p => p.PermissionCode).ToList());
    }
}
