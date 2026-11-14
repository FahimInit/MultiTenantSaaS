using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Application.Common.Models;
using SaaS.Domain.Audit;
using SaaS.Domain.Subscriptions;

namespace SaaS.Application.Modules.Subscriptions;

public sealed record CurrentSubscriptionDto(
    Guid SubscriptionId,
    SubscriptionPlanDto Plan,
    SubscriptionStatus Status,
    BillingCycle BillingCycle,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    DateTimeOffset? TrialEndsAtUtc,
    DateTimeOffset? CanceledAtUtc,
    bool AutoRenew,
    bool IsTrialExpired,
    bool AllowsWorkspaceWrites,
    int DaysRemaining,
    DashboardUsageDto Usage);

public sealed record ListSubscriptionPlansQuery : IQuery<IReadOnlyList<SubscriptionPlanDto>>;
public sealed record GetCurrentSubscriptionQuery : IQuery<CurrentSubscriptionDto>;
public sealed record ChangeSubscriptionPlanCommand(
    string PlanCode,
    BillingCycle BillingCycle,
    string? PaymentMethodToken = null) : ICommand<CurrentSubscriptionDto>;
public sealed record CancelSubscriptionCommand(string? Reason = null) : ICommand<CurrentSubscriptionDto>;

public sealed record GetSubscriptionHistoryQuery(
    int Page = 1,
    int PageSize = PaginationQuery.DefaultPageSize) : PaginationQuery, IQuery<PagedResult<SubscriptionHistoryDto>>
{
    public new int Page { get; init; } = Page;
    public new int PageSize { get; init; } = PageSize;
}

public sealed class ChangeSubscriptionPlanValidator : AbstractValidator<ChangeSubscriptionPlanCommand>
{
    public ChangeSubscriptionPlanValidator()
    {
        RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.BillingCycle).IsInEnum();
    }
}

public sealed class CancelSubscriptionValidator : AbstractValidator<CancelSubscriptionCommand>
{
    public CancelSubscriptionValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

internal static class UsageCalculator
{
    public static double? CalculatePercent(long used, int? max)
    {
        if (!max.HasValue || max.Value <= 0)
        {
            return null;
        }

        return Math.Round(used * 100.0 / max.Value, 1, MidpointRounding.AwayFromZero);
    }

    public static int CalculateDaysRemaining(DateTimeOffset periodEndUtc, DateTimeOffset utcNow)
    {
        var days = (int)Math.Ceiling((periodEndUtc - utcNow).TotalDays);
        return Math.Max(0, days);
    }

    public static CurrentSubscriptionDto BuildCurrentDto(
        TenantSubscription subscription,
        SubscriptionPlan plan,
        int activeMembers,
        int customRoles,
        long monthlyApiCalls,
        DateTimeOffset utcNow)
    {
        var effectiveEnd = subscription.Status == SubscriptionStatus.Trialing && subscription.TrialEndsAtUtc.HasValue
            ? subscription.TrialEndsAtUtc.Value
            : subscription.CurrentPeriodEndUtc;

        return new CurrentSubscriptionDto(
            subscription.Id,
            new SubscriptionPlanDto(
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
                plan.SortOrder),
            subscription.Status,
            subscription.BillingCycle,
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc,
            subscription.TrialEndsAtUtc,
            subscription.CanceledAtUtc,
            subscription.AutoRenew,
            subscription.IsTrialExpiredAt(utcNow),
            subscription.AllowsWorkspaceWritesAt(utcNow),
            CalculateDaysRemaining(effectiveEnd, utcNow),
            new DashboardUsageDto(
                activeMembers,
                plan.MaxMembers,
                CalculatePercent(activeMembers, plan.MaxMembers),
                customRoles,
                plan.MaxCustomRoles,
                CalculatePercent(customRoles, plan.MaxCustomRoles),
                monthlyApiCalls,
                plan.MaxMonthlyApiCalls,
                CalculatePercent(monthlyApiCalls, plan.MaxMonthlyApiCalls)));
    }
}

public sealed class ListSubscriptionPlansHandler
    : IQueryHandler<ListSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    private readonly ISubscriptionStore _subscriptionStore;

    public ListSubscriptionPlansHandler(ISubscriptionStore subscriptionStore)
    {
        _subscriptionStore = subscriptionStore;
    }

    public Task<IReadOnlyList<SubscriptionPlanDto>> HandleAsync(
        ListSubscriptionPlansQuery query,
        CancellationToken cancellationToken = default) =>
        _subscriptionStore.ListPlansAsync(publicAndActiveOnly: true, cancellationToken);
}

public sealed class GetCurrentSubscriptionHandler
    : IQueryHandler<GetCurrentSubscriptionQuery, CurrentSubscriptionDto>
{
    private readonly ITenantContext _tenantContext;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IMembershipStore _membershipStore;
    private readonly IRoleStore _roleStore;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCurrentSubscriptionHandler(
        ITenantContext tenantContext,
        ISubscriptionStore subscriptionStore,
        IMembershipStore membershipStore,
        IRoleStore roleStore,
        IDateTimeProvider dateTimeProvider)
    {
        _tenantContext = tenantContext;
        _subscriptionStore = subscriptionStore;
        _membershipStore = membershipStore;
        _roleStore = roleStore;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<CurrentSubscriptionDto> HandleAsync(
        GetCurrentSubscriptionQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var now = _dateTimeProvider.UtcNow;

        var current = await _subscriptionStore.GetTenantSubscriptionWithPlanAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Subscription was not found for this organization.", "subscription_not_found");

        var (periodStart, _) = TenantUsageSnapshot.GetMonthlyPeriod(now);
        var activeMembers = await _membershipStore.CountActiveMembersAsync(tenantId, cancellationToken);
        var customRoles = await _roleStore.CountCustomRolesAsync(tenantId, cancellationToken);
        var snapshot = await _subscriptionStore.GetUsageSnapshotAsync(tenantId, periodStart, cancellationToken);

        return UsageCalculator.BuildCurrentDto(
            current.Subscription,
            current.Plan,
            activeMembers,
            customRoles,
            snapshot?.ApiCallsCount ?? 0,
            now);
    }
}

public sealed class ChangeSubscriptionPlanHandler
    : ICommandHandler<ChangeSubscriptionPlanCommand, CurrentSubscriptionDto>
{
    private readonly IValidator<ChangeSubscriptionPlanCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantStore _tenantStore;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IMembershipStore _membershipStore;
    private readonly IInvitationStore _invitationStore;
    private readonly IRoleStore _roleStore;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public ChangeSubscriptionPlanHandler(
        IValidator<ChangeSubscriptionPlanCommand> validator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ITenantStore tenantStore,
        ISubscriptionStore subscriptionStore,
        IMembershipStore membershipStore,
        IInvitationStore invitationStore,
        IRoleStore roleStore,
        IPaymentProvider paymentProvider,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _tenantStore = tenantStore;
        _subscriptionStore = subscriptionStore;
        _membershipStore = membershipStore;
        _invitationStore = invitationStore;
        _roleStore = roleStore;
        _paymentProvider = paymentProvider;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<CurrentSubscriptionDto> HandleAsync(
        ChangeSubscriptionPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        var tenant = await _tenantStore.GetCurrentTenantAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization was not found.", "tenant_not_found");

        var current = await _subscriptionStore.GetTenantSubscriptionWithPlanAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Subscription was not found for this organization.", "subscription_not_found");

        var normalizedCode = command.PlanCode.Trim().ToUpperInvariant();
        var targetPlan = await _subscriptionStore.FindPlanByCodeAsync(normalizedCode, cancellationToken)
            ?? throw new NotFoundAppException($"Subscription plan '{normalizedCode}' was not found.", "plan_not_found");

        if (!targetPlan.IsActive)
        {
            throw new ConflictAppException(
                $"Subscription plan '{targetPlan.Name}' is no longer available for selection.",
                "plan_inactive");
        }

        var activeMembers = await _membershipStore.CountActiveMembersAsync(tenantId, cancellationToken);
        var pendingInvites = await _invitationStore.CountPendingInvitationsAsync(tenantId, cancellationToken);
        var customRoles = await _roleStore.CountCustomRolesAsync(tenantId, cancellationToken);

        if (targetPlan.MaxMembers.HasValue && activeMembers + pendingInvites > targetPlan.MaxMembers.Value)
        {
            throw new ConflictAppException(
                $"Cannot switch to '{targetPlan.Name}' because your organization currently occupies {activeMembers + pendingInvites} seats (including pending invitations), exceeding the plan limit of {targetPlan.MaxMembers.Value}.",
                "downgrade_member_limit_conflict");
        }

        if ((!targetPlan.AllowCustomRoles && customRoles > 0) ||
            (targetPlan.MaxCustomRoles.HasValue && customRoles > targetPlan.MaxCustomRoles.Value))
        {
            throw new ConflictAppException(
                $"Cannot switch to '{targetPlan.Name}' while {customRoles} custom role(s) exist. Remove excess custom roles first.",
                "downgrade_custom_roles_conflict");
        }

        var amountCents = targetPlan.GetPriceCents(command.BillingCycle);
        string? externalSubId = null;
        var paymentReference = "FREE_TIER";

        if (amountCents > 0)
        {
            var paymentResult = await _paymentProvider.ProcessSubscriptionPaymentAsync(
                new PaymentChargeRequest(
                    tenant.Id,
                    tenant.Slug.Value,
                    targetPlan.Code,
                    command.BillingCycle,
                    amountCents,
                    "USD",
                    command.PaymentMethodToken),
                cancellationToken);

            if (!paymentResult.Succeeded)
            {
                throw new ForbiddenAppException(
                    paymentResult.FailureReason ?? "Payment authorization failed.",
                    "payment_failed");
            }

            externalSubId = paymentResult.ExternalSubscriptionId;
            paymentReference = paymentResult.PaymentReference;
        }
        else if (!string.IsNullOrWhiteSpace(current.Subscription.ExternalProviderSubscriptionId))
        {
            await _paymentProvider.CancelSubscriptionAsync(
                current.Subscription.ExternalProviderSubscriptionId,
                cancellationToken);
        }

        var now = _dateTimeProvider.UtcNow;
        var previousPlan = current.Plan;

        var eventType = previousPlan.Id == targetPlan.Id
            ? SubscriptionEventType.Renewed
            : targetPlan.MonthlyPriceCents > previousPlan.MonthlyPriceCents
                ? SubscriptionEventType.Upgraded
                : targetPlan.MonthlyPriceCents < previousPlan.MonthlyPriceCents
                    ? SubscriptionEventType.Downgraded
                    : SubscriptionEventType.Subscribed;

        var history = new SubscriptionHistory(
            tenantId,
            current.Subscription.Id,
            previousPlan.Id,
            targetPlan.Id,
            eventType,
            amountCents,
            "USD",
            paymentReference,
            actorId,
            $"Plan changed from {previousPlan.Name} to {targetPlan.Name} ({command.BillingCycle}).",
            now);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            current.Subscription.ChangePlan(
                targetPlan.Id,
                command.BillingCycle,
                isPaidPlan: amountCents > 0,
                externalProviderSubscriptionId: externalSubId,
                utcNow: now);

            if (tenant.Status == Domain.Tenants.TenantStatus.Trial)
            {
                tenant.Activate(now);
            }

            await _subscriptionStore.AddSubscriptionHistoryAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.Subscription,
            "SUBSCRIPTION_PLAN_CHANGED",
            "TenantSubscription",
            current.Subscription.Id.ToString(),
            $"Changed subscription plan from '{previousPlan.Name}' to '{targetPlan.Name}' ({command.BillingCycle}).",
            new Dictionary<string, object?>
            {
                ["fromPlanCode"] = previousPlan.Code,
                ["toPlanCode"] = targetPlan.Code,
                ["billingCycle"] = command.BillingCycle.ToString(),
                ["amountCents"] = amountCents,
                ["paymentReference"] = paymentReference
            },
            cancellationToken);

        var (periodStart, _) = TenantUsageSnapshot.GetMonthlyPeriod(now);
        var snapshot = await _subscriptionStore.GetUsageSnapshotAsync(tenantId, periodStart, cancellationToken);

        return UsageCalculator.BuildCurrentDto(
            current.Subscription,
            targetPlan,
            activeMembers,
            customRoles,
            snapshot?.ApiCallsCount ?? 0,
            now);
    }
}

public sealed class CancelSubscriptionHandler
    : ICommandHandler<CancelSubscriptionCommand, CurrentSubscriptionDto>
{
    private readonly IValidator<CancelSubscriptionCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IMembershipStore _membershipStore;
    private readonly IRoleStore _roleStore;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public CancelSubscriptionHandler(
        IValidator<CancelSubscriptionCommand> validator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ISubscriptionStore subscriptionStore,
        IMembershipStore membershipStore,
        IRoleStore roleStore,
        IPaymentProvider paymentProvider,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _subscriptionStore = subscriptionStore;
        _membershipStore = membershipStore;
        _roleStore = roleStore;
        _paymentProvider = paymentProvider;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<CurrentSubscriptionDto> HandleAsync(
        CancelSubscriptionCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        var actorId = _currentUser.UserId ?? throw new UnauthorizedAppException();
        var actorEmail = _currentUser.Email ?? "unknown";

        var current = await _subscriptionStore.GetTenantSubscriptionWithPlanAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Subscription was not found.", "subscription_not_found");

        if (!string.IsNullOrWhiteSpace(current.Subscription.ExternalProviderSubscriptionId))
        {
            await _paymentProvider.CancelSubscriptionAsync(
                current.Subscription.ExternalProviderSubscriptionId,
                cancellationToken);
        }

        var now = _dateTimeProvider.UtcNow;
        current.Subscription.CancelAutoRenew(now);

        var history = new SubscriptionHistory(
            tenantId,
            current.Subscription.Id,
            current.Plan.Id,
            current.Plan.Id,
            SubscriptionEventType.Canceled,
            0,
            "USD",
            null,
            actorId,
            string.IsNullOrWhiteSpace(command.Reason)
                ? "Auto-renewal canceled by organization administrator."
                : $"Canceled: {command.Reason.Trim()}",
            now);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _subscriptionStore.AddSubscriptionHistoryAsync(history, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            actorId,
            actorEmail,
            AuditCategory.Subscription,
            "SUBSCRIPTION_CANCELED",
            "TenantSubscription",
            current.Subscription.Id.ToString(),
            $"Canceled auto-renewal on '{current.Plan.Name}' subscription.",
            cancellationToken: cancellationToken);

        var activeMembers = await _membershipStore.CountActiveMembersAsync(tenantId, cancellationToken);
        var customRoles = await _roleStore.CountCustomRolesAsync(tenantId, cancellationToken);
        var (periodStart, _) = TenantUsageSnapshot.GetMonthlyPeriod(now);
        var snapshot = await _subscriptionStore.GetUsageSnapshotAsync(tenantId, periodStart, cancellationToken);

        return UsageCalculator.BuildCurrentDto(
            current.Subscription,
            current.Plan,
            activeMembers,
            customRoles,
            snapshot?.ApiCallsCount ?? 0,
            now);
    }
}

public sealed class GetSubscriptionHistoryHandler
    : IQueryHandler<GetSubscriptionHistoryQuery, PagedResult<SubscriptionHistoryDto>>
{
    private readonly ITenantContext _tenantContext;
    private readonly ISubscriptionStore _subscriptionStore;

    public GetSubscriptionHistoryHandler(ITenantContext tenantContext, ISubscriptionStore subscriptionStore)
    {
        _tenantContext = tenantContext;
        _subscriptionStore = subscriptionStore;
    }

    public Task<PagedResult<SubscriptionHistoryDto>> HandleAsync(
        GetSubscriptionHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();
        return _subscriptionStore.ListSubscriptionHistoryAsync(
            tenantId,
            query.NormalizedPage,
            query.NormalizedPageSize,
            cancellationToken);
    }
}
