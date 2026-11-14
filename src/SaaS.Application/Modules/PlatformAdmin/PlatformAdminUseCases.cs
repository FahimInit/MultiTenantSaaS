using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Application.Common.Models;
using SaaS.Domain.Audit;
using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;

namespace SaaS.Application.Modules.PlatformAdmin;

public sealed record GetPlatformDashboardQuery : IQuery<PlatformDashboardDto>;

public sealed record ListPlatformTenantsQuery(
    int Page = 1,
    int PageSize = PaginationQuery.DefaultPageSize,
    string? Search = null,
    TenantStatus? Status = null,
    string SortDirection = "desc") : PaginationQuery, IQuery<PagedResult<PlatformTenantListItemDto>>
{
    public new int Page { get; init; } = Page;
    public new int PageSize { get; init; } = PageSize;
    public new string? Search { get; init; } = Search;
    public new string SortDirection { get; init; } = SortDirection;
}

public sealed record GetPlatformTenantDetailQuery(Guid TenantId) : IQuery<PlatformTenantDetailDto>;
public sealed record SuspendTenantCommand(Guid TenantId, string Reason) : ICommand<PlatformTenantDetailDto>;
public sealed record ReactivateTenantCommand(Guid TenantId) : ICommand<PlatformTenantDetailDto>;

public sealed record ListPlatformPlansQuery : IQuery<IReadOnlyList<SubscriptionPlanDto>>;

public sealed record CreatePlatformPlanCommand(
    string Code,
    string Name,
    string Description,
    long MonthlyPriceCents,
    long AnnualPriceCents,
    int TrialDays,
    int? MaxMembers,
    int? MaxCustomRoles,
    int? MaxMonthlyApiCalls,
    int AuditRetentionDays,
    bool AllowCustomRoles,
    bool IsActive,
    bool IsPublic,
    int SortOrder) : ICommand<SubscriptionPlanDto>;

public sealed record UpdatePlatformPlanCommand(
    Guid PlanId,
    string Name,
    string Description,
    long MonthlyPriceCents,
    long AnnualPriceCents,
    int TrialDays,
    int? MaxMembers,
    int? MaxCustomRoles,
    int? MaxMonthlyApiCalls,
    int AuditRetentionDays,
    bool AllowCustomRoles,
    bool IsActive,
    bool IsPublic,
    int SortOrder) : ICommand<SubscriptionPlanDto>;

public sealed record GetPlatformAuditLogsQuery(
    int Page = 1,
    int PageSize = PaginationQuery.DefaultPageSize,
    Guid? TenantId = null,
    AuditScope? Scope = null,
    AuditCategory? Category = null,
    string? Action = null,
    string? Search = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string SortDirection = "desc") : PaginationQuery, IQuery<PagedResult<AuditLogDto>>
{
    public new int Page { get; init; } = Page;
    public new int PageSize { get; init; } = PageSize;
    public new string? Search { get; init; } = Search;
    public new string SortDirection { get; init; } = SortDirection;
}

public sealed class SuspendTenantValidator : AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(300);
    }
}

public sealed class CreatePlatformPlanValidator : AbstractValidator<CreatePlatformPlanCommand>
{
    public CreatePlatformPlanValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64).Matches("^[A-Za-z0-9_]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.MonthlyPriceCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AnnualPriceCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AuditRetentionDays).GreaterThanOrEqualTo(1);
    }
}

public sealed class UpdatePlatformPlanValidator : AbstractValidator<UpdatePlatformPlanCommand>
{
    public UpdatePlatformPlanValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.MonthlyPriceCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AnnualPriceCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AuditRetentionDays).GreaterThanOrEqualTo(1);
    }
}

public sealed class GetPlatformDashboardHandler : IQueryHandler<GetPlatformDashboardQuery, PlatformDashboardDto>
{
    private readonly IPlatformAdminStore _platformAdminStore;

    public GetPlatformDashboardHandler(IPlatformAdminStore platformAdminStore)
    {
        _platformAdminStore = platformAdminStore;
    }

    public Task<PlatformDashboardDto> HandleAsync(
        GetPlatformDashboardQuery query,
        CancellationToken cancellationToken = default) =>
        _platformAdminStore.GetPlatformDashboardAsync(cancellationToken);
}

public sealed class ListPlatformTenantsHandler
    : IQueryHandler<ListPlatformTenantsQuery, PagedResult<PlatformTenantListItemDto>>
{
    private readonly IPlatformAdminStore _platformAdminStore;

    public ListPlatformTenantsHandler(IPlatformAdminStore platformAdminStore)
    {
        _platformAdminStore = platformAdminStore;
    }

    public Task<PagedResult<PlatformTenantListItemDto>> HandleAsync(
        ListPlatformTenantsQuery query,
        CancellationToken cancellationToken = default) =>
        _platformAdminStore.ListTenantsAsync(
            new PlatformTenantFilterQuery(
                query.NormalizedPage,
                query.NormalizedPageSize,
                query.Search,
                query.Status,
                query.IsAscending),
            cancellationToken);
}

public sealed class GetPlatformTenantDetailHandler
    : IQueryHandler<GetPlatformTenantDetailQuery, PlatformTenantDetailDto>
{
    private readonly IPlatformAdminStore _platformAdminStore;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetPlatformTenantDetailHandler(
        IPlatformAdminStore platformAdminStore,
        IDateTimeProvider dateTimeProvider)
    {
        _platformAdminStore = platformAdminStore;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PlatformTenantDetailDto> HandleAsync(
        GetPlatformTenantDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _platformAdminStore.GetTenantDetailAsync(
                query.TenantId,
                _dateTimeProvider.UtcNow,
                cancellationToken)
            ?? throw new NotFoundAppException("Organization was not found.", "tenant_not_found");
    }
}

public sealed class SuspendTenantHandler : ICommandHandler<SuspendTenantCommand, PlatformTenantDetailDto>
{
    private readonly IValidator<SuspendTenantCommand> _validator;
    private readonly IPlatformAdminStore _platformAdminStore;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public SuspendTenantHandler(
        IValidator<SuspendTenantCommand> validator,
        IPlatformAdminStore platformAdminStore,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _platformAdminStore = platformAdminStore;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<PlatformTenantDetailDto> HandleAsync(
        SuspendTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenant = await _platformAdminStore.FindTenantForUpdateAsync(command.TenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization was not found.", "tenant_not_found");

        var now = _dateTimeProvider.UtcNow;
        tenant.Suspend(command.Reason, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogPlatformAsync(
            _currentUser.UserId,
            _currentUser.Email ?? "platform-admin",
            AuditCategory.Platform,
            "PLATFORM_TENANT_SUSPENDED",
            "Tenant",
            tenant.Id.ToString(),
            $"Platform administrator suspended organization '{tenant.Name}'.",
            relatedTenantId: tenant.Id,
            metadata: new Dictionary<string, object?> { ["reason"] = command.Reason.Trim() },
            cancellationToken: cancellationToken);

        return (await _platformAdminStore.GetTenantDetailAsync(tenant.Id, now, cancellationToken))!;
    }
}

public sealed class ReactivateTenantHandler : ICommandHandler<ReactivateTenantCommand, PlatformTenantDetailDto>
{
    private readonly IPlatformAdminStore _platformAdminStore;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public ReactivateTenantHandler(
        IPlatformAdminStore platformAdminStore,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _platformAdminStore = platformAdminStore;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<PlatformTenantDetailDto> HandleAsync(
        ReactivateTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _platformAdminStore.FindTenantForUpdateAsync(command.TenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization was not found.", "tenant_not_found");

        var now = _dateTimeProvider.UtcNow;
        tenant.Activate(now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogPlatformAsync(
            _currentUser.UserId,
            _currentUser.Email ?? "platform-admin",
            AuditCategory.Platform,
            "PLATFORM_TENANT_REACTIVATED",
            "Tenant",
            tenant.Id.ToString(),
            $"Platform administrator reactivated organization '{tenant.Name}'.",
            relatedTenantId: tenant.Id,
            cancellationToken: cancellationToken);

        return (await _platformAdminStore.GetTenantDetailAsync(tenant.Id, now, cancellationToken))!;
    }
}

public sealed class ListPlatformPlansHandler
    : IQueryHandler<ListPlatformPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    private readonly ISubscriptionStore _subscriptionStore;

    public ListPlatformPlansHandler(ISubscriptionStore subscriptionStore)
    {
        _subscriptionStore = subscriptionStore;
    }

    public Task<IReadOnlyList<SubscriptionPlanDto>> HandleAsync(
        ListPlatformPlansQuery query,
        CancellationToken cancellationToken = default) =>
        _subscriptionStore.ListPlansAsync(publicAndActiveOnly: false, cancellationToken);
}

public sealed class CreatePlatformPlanHandler
    : ICommandHandler<CreatePlatformPlanCommand, SubscriptionPlanDto>
{
    private readonly IValidator<CreatePlatformPlanCommand> _validator;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public CreatePlatformPlanHandler(
        IValidator<CreatePlatformPlanCommand> validator,
        ISubscriptionStore subscriptionStore,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _subscriptionStore = subscriptionStore;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public async Task<SubscriptionPlanDto> HandleAsync(
        CreatePlatformPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await _subscriptionStore.PlanCodeExistsAsync(normalizedCode, null, cancellationToken))
        {
            throw new ConflictAppException(
                $"A subscription plan with code '{normalizedCode}' already exists.",
                "duplicate_plan_code");
        }

        var plan = new SubscriptionPlan(
            normalizedCode,
            command.Name,
            command.Description,
            command.MonthlyPriceCents,
            command.AnnualPriceCents,
            command.TrialDays,
            command.MaxMembers,
            command.MaxCustomRoles,
            command.MaxMonthlyApiCalls,
            command.AuditRetentionDays,
            command.AllowCustomRoles,
            command.IsActive,
            command.IsPublic,
            command.SortOrder);

        await _subscriptionStore.AddPlanAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogPlatformAsync(
            _currentUser.UserId,
            _currentUser.Email ?? "platform-admin",
            AuditCategory.Platform,
            "PLATFORM_PLAN_CREATED",
            "SubscriptionPlan",
            plan.Id.ToString(),
            $"Created subscription plan '{plan.Code}' ({plan.Name}).",
            cancellationToken: cancellationToken);

        return MapPlanDto(plan);
    }

    internal static SubscriptionPlanDto MapPlanDto(SubscriptionPlan plan) =>
        new(
            plan.Id,
            plan.Code,
            plan.Name,
            plan.Description,
            plan.MonthlyPriceCents,
            plan.AnnualPriceCents,
            plan.TrialDays,
            plan.MaxMembers,
            plan.MaxCustomRoles,
            plan.MaxMonthlyApiCalls,
            plan.AuditRetentionDays,
            plan.AllowCustomRoles,
            plan.IsActive,
            plan.IsPublic,
            plan.SortOrder);
}

public sealed class UpdatePlatformPlanHandler
    : ICommandHandler<UpdatePlatformPlanCommand, SubscriptionPlanDto>
{
    private readonly IValidator<UpdatePlatformPlanCommand> _validator;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public UpdatePlatformPlanHandler(
        IValidator<UpdatePlatformPlanCommand> validator,
        ISubscriptionStore subscriptionStore,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _subscriptionStore = subscriptionStore;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public async Task<SubscriptionPlanDto> HandleAsync(
        UpdatePlatformPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var plan = await _subscriptionStore.FindPlanByIdAsync(command.PlanId, cancellationToken)
            ?? throw new NotFoundAppException("Subscription plan was not found.", "plan_not_found");

        plan.UpdateDetails(
            command.Name,
            command.Description,
            command.MonthlyPriceCents,
            command.AnnualPriceCents,
            command.TrialDays,
            command.MaxMembers,
            command.MaxCustomRoles,
            command.MaxMonthlyApiCalls,
            command.AuditRetentionDays,
            command.AllowCustomRoles,
            command.IsActive,
            command.IsPublic,
            command.SortOrder);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogPlatformAsync(
            _currentUser.UserId,
            _currentUser.Email ?? "platform-admin",
            AuditCategory.Platform,
            "PLATFORM_PLAN_UPDATED",
            "SubscriptionPlan",
            plan.Id.ToString(),
            $"Updated subscription plan '{plan.Code}'.",
            cancellationToken: cancellationToken);

        return CreatePlatformPlanHandler.MapPlanDto(plan);
    }
}

public sealed class GetPlatformAuditLogsHandler
    : IQueryHandler<GetPlatformAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly IAuditQueryStore _auditQueryStore;

    public GetPlatformAuditLogsHandler(IAuditQueryStore auditQueryStore)
    {
        _auditQueryStore = auditQueryStore;
    }

    public Task<PagedResult<AuditLogDto>> HandleAsync(
        GetPlatformAuditLogsQuery query,
        CancellationToken cancellationToken = default) =>
        _auditQueryStore.QueryPlatformLogsAsync(
            new PlatformAuditFilterQuery(
                query.NormalizedPage,
                query.NormalizedPageSize,
                query.TenantId,
                query.Scope,
                query.Category,
                query.Action,
                query.Search,
                query.FromUtc,
                query.ToUtc,
                query.IsAscending),
            cancellationToken);
}
