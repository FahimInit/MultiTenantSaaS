using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;

namespace SaaS.Application.Modules.Tenants;

public sealed record GetMyTenantsQuery : IQuery<IReadOnlyList<UserTenantSummaryDto>>;

public sealed class GetMyTenantsHandler : IQueryHandler<GetMyTenantsQuery, IReadOnlyList<UserTenantSummaryDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly ITenantStore _tenantStore;

    public GetMyTenantsHandler(ICurrentUser currentUser, ITenantStore tenantStore)
    {
        _currentUser = currentUser;
        _tenantStore = tenantStore;
    }

    public async Task<IReadOnlyList<UserTenantSummaryDto>> HandleAsync(
        GetMyTenantsQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        return await _tenantStore.ListUserTenantsAsync(userId, cancellationToken);
    }
}

public sealed record GetCurrentTenantQuery : IQuery<TenantDetailDto>;

public sealed class GetCurrentTenantHandler : IQueryHandler<GetCurrentTenantQuery, TenantDetailDto>
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantStore _tenantStore;

    public GetCurrentTenantHandler(ITenantContext tenantContext, ITenantStore tenantStore)
    {
        _tenantContext = tenantContext;
        _tenantStore = tenantStore;
    }

    public async Task<TenantDetailDto> HandleAsync(
        GetCurrentTenantQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new ForbiddenAppException("Tenant context is not resolved.", "tenant_context_missing");

        var tenant = await _tenantStore.GetCurrentTenantAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization was not found.", "tenant_not_found");

        return new TenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug.Value,
            tenant.Status,
            tenant.IsOnboardingCompleted,
            tenant.OnboardingCompletedAtUtc,
            tenant.CreatedAtUtc,
            _tenantContext.RoleCode ?? string.Empty,
            _tenantContext.Permissions.OrderBy(p => p).ToList());
    }
}

public sealed record UpdateCurrentTenantCommand(string Name, string Slug) : ICommand<TenantDetailDto>;

public sealed class UpdateCurrentTenantValidator : AbstractValidator<UpdateCurrentTenantCommand>
{
    public UpdateCurrentTenantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Slug).NotEmpty().MinimumLength(3).MaximumLength(63);
    }
}

public sealed class UpdateCurrentTenantHandler : ICommandHandler<UpdateCurrentTenantCommand, TenantDetailDto>
{
    private readonly IValidator<UpdateCurrentTenantCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantStore _tenantStore;
    private readonly ISubscriptionQuotaService _quotaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public UpdateCurrentTenantHandler(
        IValidator<UpdateCurrentTenantCommand> validator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ITenantStore tenantStore,
        ISubscriptionQuotaService quotaService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _tenantStore = tenantStore;
        _quotaService = quotaService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<TenantDetailDto> HandleAsync(
        UpdateCurrentTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = _tenantContext.TenantId
            ?? throw new ForbiddenAppException("Tenant context is not resolved.", "tenant_context_missing");

        await _quotaService.EnsureWorkspaceWriteAllowedAsync(tenantId, cancellationToken);

        var tenant = await _tenantStore.GetCurrentTenantAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization was not found.", "tenant_not_found");

        var now = _dateTimeProvider.UtcNow;
        tenant.UpdateProfile(command.Name, command.Slug, now);

        if (await _tenantStore.SlugExistsAsync(tenant.Slug.Value, excludeTenantId: tenant.Id, cancellationToken))
        {
            throw new ConflictAppException(
                $"An organization with slug '{tenant.Slug.Value}' already exists.",
                "duplicate_tenant_slug");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenant.Id,
            _currentUser.UserId,
            _currentUser.Email ?? "unknown",
            AuditCategory.Tenant,
            "TENANT_UPDATED",
            "Tenant",
            tenant.Id.ToString(),
            $"Organization profile updated to '{tenant.Name}' ({tenant.Slug.Value}).",
            cancellationToken: cancellationToken);

        return new TenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug.Value,
            tenant.Status,
            tenant.IsOnboardingCompleted,
            tenant.OnboardingCompletedAtUtc,
            tenant.CreatedAtUtc,
            _tenantContext.RoleCode ?? string.Empty,
            _tenantContext.Permissions.OrderBy(p => p).ToList());
    }
}

public sealed record CompleteOnboardingCommand : ICommand<TenantDetailDto>;

public sealed class CompleteOnboardingHandler : ICommandHandler<CompleteOnboardingCommand, TenantDetailDto>
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantStore _tenantStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public CompleteOnboardingHandler(
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ITenantStore tenantStore,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _tenantStore = tenantStore;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<TenantDetailDto> HandleAsync(
        CompleteOnboardingCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new ForbiddenAppException("Tenant context is not resolved.", "tenant_context_missing");

        var tenant = await _tenantStore.GetCurrentTenantAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization was not found.", "tenant_not_found");

        var now = _dateTimeProvider.UtcNow;
        tenant.CompleteOnboarding(now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenant.Id,
            _currentUser.UserId,
            _currentUser.Email ?? "unknown",
            AuditCategory.Tenant,
            "TENANT_ONBOARDING_COMPLETED",
            "Tenant",
            tenant.Id.ToString(),
            "Organization onboarding marked completed.",
            cancellationToken: cancellationToken);

        return new TenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug.Value,
            tenant.Status,
            tenant.IsOnboardingCompleted,
            tenant.OnboardingCompletedAtUtc,
            tenant.CreatedAtUtc,
            _tenantContext.RoleCode ?? string.Empty,
            _tenantContext.Permissions.OrderBy(p => p).ToList());
    }
}
