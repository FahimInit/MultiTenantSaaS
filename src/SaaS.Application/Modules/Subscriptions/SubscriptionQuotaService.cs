using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Subscriptions;

namespace SaaS.Application.Modules.Subscriptions;

public sealed class SubscriptionQuotaService : ISubscriptionQuotaService, ISubscriptionProvisioner
{
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IMembershipStore _membershipStore;
    private readonly IInvitationStore _invitationStore;
    private readonly IRoleStore _roleStore;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SubscriptionQuotaService(
        ISubscriptionStore subscriptionStore,
        IMembershipStore membershipStore,
        IInvitationStore invitationStore,
        IRoleStore roleStore,
        IDateTimeProvider dateTimeProvider)
    {
        _subscriptionStore = subscriptionStore;
        _membershipStore = membershipStore;
        _invitationStore = invitationStore;
        _roleStore = roleStore;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task EnsureWorkspaceWriteAllowedAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var current = await GetRequiredSubscriptionAsync(tenantId, cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        if (!current.Subscription.AllowsWorkspaceWritesAt(now))
        {
            throw new QuotaExceededAppException(
                "Your organization's trial or subscription has expired. Upgrade or renew a plan to continue making workspace changes.",
                "subscription_inactive_or_expired");
        }
    }

    public async Task EnsureCanAddMemberOrInviteAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var current = await GetRequiredSubscriptionAsync(tenantId, cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        if (!current.Subscription.AllowsWorkspaceWritesAt(now))
        {
            throw new QuotaExceededAppException(
                "Your organization's trial or subscription has expired. Upgrade or renew a plan before inviting members.",
                "subscription_inactive_or_expired");
        }

        if (!current.Plan.MaxMembers.HasValue)
        {
            return;
        }

        var activeMembers = await _membershipStore.CountActiveMembersAsync(tenantId, cancellationToken);
        var pendingInvites = await _invitationStore.CountPendingInvitationsAsync(tenantId, cancellationToken);

        if (activeMembers + pendingInvites >= current.Plan.MaxMembers.Value)
        {
            throw new QuotaExceededAppException(
                $"Your current '{current.Plan.Name}' plan supports up to {current.Plan.MaxMembers.Value} members and pending invitations. Upgrade your subscription to add more seats.",
                "member_quota_exceeded");
        }
    }

    public async Task EnsureCanCreateCustomRoleAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var current = await GetRequiredSubscriptionAsync(tenantId, cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        if (!current.Subscription.AllowsWorkspaceWritesAt(now))
        {
            throw new QuotaExceededAppException(
                "Your organization's trial or subscription has expired. Upgrade or renew a plan before creating roles.",
                "subscription_inactive_or_expired");
        }

        if (!current.Plan.AllowCustomRoles)
        {
            throw new QuotaExceededAppException(
                $"Custom roles are not available on the '{current.Plan.Name}' plan. Upgrade to Pro or Enterprise to define custom roles.",
                "custom_roles_not_allowed");
        }

        if (!current.Plan.MaxCustomRoles.HasValue)
        {
            return;
        }

        var customRolesCount = await _roleStore.CountCustomRolesAsync(tenantId, cancellationToken);
        if (customRolesCount >= current.Plan.MaxCustomRoles.Value)
        {
            throw new QuotaExceededAppException(
                $"Your '{current.Plan.Name}' plan allows up to {current.Plan.MaxCustomRoles.Value} custom roles. Upgrade to add more.",
                "custom_role_quota_exceeded");
        }
    }

    public async Task SyncUsageCountsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var (periodStart, periodEnd) = TenantUsageSnapshot.GetMonthlyPeriod(now);

        var activeMembers = await _membershipStore.CountActiveMembersAsync(tenantId, cancellationToken);
        var customRoles = await _roleStore.CountCustomRolesAsync(tenantId, cancellationToken);
        var snapshot = await _subscriptionStore.GetUsageSnapshotAsync(tenantId, periodStart, cancellationToken);

        await UpsertUsageSnapshotAsync(tenantId, periodStart, periodEnd, activeMembers, customRoles, snapshot, now, cancellationToken);
    }

    public async Task SyncUsageCountsUnfilteredAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var (periodStart, periodEnd) = TenantUsageSnapshot.GetMonthlyPeriod(now);

        var activeMembers = await _membershipStore.CountActiveMembersUnfilteredAsync(tenantId, cancellationToken);
        var customRoles = await _roleStore.CountCustomRolesUnfilteredAsync(tenantId, cancellationToken);
        var snapshot = await _subscriptionStore.GetUsageSnapshotUnfilteredAsync(tenantId, periodStart, cancellationToken);

        await UpsertUsageSnapshotAsync(tenantId, periodStart, periodEnd, activeMembers, customRoles, snapshot, now, cancellationToken);
    }

    private async Task UpsertUsageSnapshotAsync(
        Guid tenantId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        int activeMembers,
        int customRoles,
        TenantUsageSnapshot? snapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (snapshot is null)
        {
            snapshot = new TenantUsageSnapshot(
                tenantId,
                periodStart,
                periodEnd,
                activeMembers,
                customRoles,
                apiCallsCount: 0,
                updatedAtUtc: now);
            await _subscriptionStore.AddUsageSnapshotAsync(snapshot, cancellationToken);
        }
        else
        {
            snapshot.UpdateCounts(activeMembers, customRoles, now);
        }
    }

    public async Task ProvisionInitialSubscriptionAsync(
        Guid tenantId,
        Guid actorUserId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var starterPlan = await _subscriptionStore.FindPlanByCodeAsync("PRO", cancellationToken)
            ?? await _subscriptionStore.FindPlanByCodeAsync("FREE", cancellationToken);

        if (starterPlan is null)
        {
            return;
        }

        TenantSubscription subscription;
        SubscriptionEventType eventType;
        string notes;

        if (!starterPlan.IsFreePlan && starterPlan.TrialDays > 0)
        {
            subscription = TenantSubscription.StartTrial(tenantId, starterPlan.Id, starterPlan.TrialDays, utcNow);
            eventType = SubscriptionEventType.TrialStarted;
            notes = $"Started {starterPlan.TrialDays}-day trial on {starterPlan.Name} plan.";
        }
        else
        {
            subscription = TenantSubscription.CreateActive(
                tenantId,
                starterPlan.Id,
                BillingCycle.Monthly,
                externalProviderSubscriptionId: null,
                utcNow: utcNow,
                autoRenew: false);
            eventType = SubscriptionEventType.Subscribed;
            notes = $"Provisioned {starterPlan.Name} plan.";
        }

        var history = new SubscriptionHistory(
            tenantId,
            subscription.Id,
            fromPlanId: null,
            toPlanId: starterPlan.Id,
            eventType: eventType,
            amountCents: 0,
            currency: "USD",
            paymentReference: "INITIAL_PROVISIONING",
            actorUserId: actorUserId,
            notes: notes,
            occurredAtUtc: utcNow);

        var (periodStart, periodEnd) = TenantUsageSnapshot.GetMonthlyPeriod(utcNow);
        var usageSnapshot = new TenantUsageSnapshot(
            tenantId,
            periodStart,
            periodEnd,
            activeMembersCount: 1,
            customRolesCount: 0,
            apiCallsCount: 0,
            updatedAtUtc: utcNow);

        await _subscriptionStore.AddTenantSubscriptionAsync(subscription, cancellationToken);
        await _subscriptionStore.AddSubscriptionHistoryAsync(history, cancellationToken);
        await _subscriptionStore.AddUsageSnapshotAsync(usageSnapshot, cancellationToken);
    }

    private async Task<TenantSubscriptionWithPlan> GetRequiredSubscriptionAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _subscriptionStore.GetTenantSubscriptionWithPlanAsync(tenantId, cancellationToken)
            ?? throw new QuotaExceededAppException(
                "No subscription record exists for this organization.",
                "subscription_missing");
    }
}
