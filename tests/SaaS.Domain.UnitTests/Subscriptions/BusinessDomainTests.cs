using SaaS.Domain.Common;
using SaaS.Domain.Invitations;
using SaaS.Domain.Subscriptions;
using Shouldly;

namespace SaaS.Domain.UnitTests.Subscriptions;

public sealed class BusinessDomainTests
{
    [Fact]
    public void TenantInvitation_WhenAcceptedByDifferentEmail_ShouldThrowDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var invite = new TenantInvitation(
            Guid.CreateVersion7(),
            "newuser@acme.local",
            Guid.CreateVersion7(),
            "HASH_ABC",
            Guid.CreateVersion7(),
            now.AddDays(7),
            now);

        var ex = Should.Throw<DomainException>(() =>
            invite.Accept(Guid.CreateVersion7(), "attacker@evil.local", now.AddHours(1)));

        ex.Code.ShouldBe("invitation_email_mismatch");
        invite.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public void TenantInvitation_WhenExpired_ShouldMarkExpiredAndThrow()
    {
        var now = DateTimeOffset.UtcNow;
        var invite = new TenantInvitation(
            Guid.CreateVersion7(),
            "newuser@acme.local",
            Guid.CreateVersion7(),
            "HASH_ABC",
            Guid.CreateVersion7(),
            now.AddHours(1),
            now);

        var ex = Should.Throw<DomainException>(() =>
            invite.Accept(Guid.CreateVersion7(), "newuser@acme.local", now.AddHours(2)));

        ex.Code.ShouldBe("invitation_expired");
        invite.Status.ShouldBe(InvitationStatus.Expired);
    }

    [Fact]
    public void TenantInvitation_WhenRevokedOrResent_ShouldTransitionCleanly()
    {
        var now = DateTimeOffset.UtcNow;
        var invite = new TenantInvitation(
            Guid.CreateVersion7(),
            "newuser@acme.local",
            Guid.CreateVersion7(),
            "HASH_1",
            Guid.CreateVersion7(),
            now.AddDays(3),
            now);

        invite.RefreshForResend("HASH_2", now.AddDays(7), now.AddMinutes(5));
        invite.TokenHash.ShouldBe("HASH_2");

        invite.Revoke(now.AddMinutes(10));
        invite.Status.ShouldBe(InvitationStatus.Revoked);
        Should.Throw<DomainException>(() => invite.Accept(Guid.CreateVersion7(), "newuser@acme.local", now.AddMinutes(11)));
    }

    [Fact]
    public void TenantSubscription_TrialExpiry_ShouldBlockWorkspaceWritesWhenExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var sub = TenantSubscription.StartTrial(Guid.CreateVersion7(), Guid.CreateVersion7(), 14, now);

        sub.Status.ShouldBe(SubscriptionStatus.Trialing);
        sub.AllowsWorkspaceWritesAt(now.AddDays(5)).ShouldBeTrue();
        sub.IsTrialExpiredAt(now.AddDays(5)).ShouldBeFalse();

        sub.AllowsWorkspaceWritesAt(now.AddDays(15)).ShouldBeFalse();
        sub.IsTrialExpiredAt(now.AddDays(15)).ShouldBeTrue();
    }

    [Fact]
    public void TenantSubscription_CancelAndPlanChange_ShouldTrackLifecycle()
    {
        var now = DateTimeOffset.UtcNow;
        var proPlanId = Guid.CreateVersion7();
        var entPlanId = Guid.CreateVersion7();

        var sub = TenantSubscription.CreateActive(Guid.CreateVersion7(), proPlanId, BillingCycle.Monthly, "sub_1", now);
        sub.CancelAutoRenew(now.AddDays(2));
        sub.Status.ShouldBe(SubscriptionStatus.Canceled);
        sub.AutoRenew.ShouldBeFalse();
        sub.AllowsWorkspaceWritesAt(now.AddDays(3)).ShouldBeTrue();
        sub.AllowsWorkspaceWritesAt(now.AddMonths(2)).ShouldBeFalse();

        sub.ChangePlan(entPlanId, BillingCycle.Annual, isPaidPlan: true, "sub_2", now.AddDays(4));
        sub.Status.ShouldBe(SubscriptionStatus.Active);
        sub.BillingCycle.ShouldBe(BillingCycle.Annual);
        sub.PlanId.ShouldBe(entPlanId);
        sub.AutoRenew.ShouldBeTrue();
    }

    [Fact]
    public void SubscriptionPlan_InvalidQuotaOrPrice_ShouldThrowDomainException()
    {
        Should.Throw<DomainException>(() =>
            new SubscriptionPlan("BAD", "Bad", "", -1, 0, 0, 5, 1, 100, 30, true, true, true, 1));

        Should.Throw<DomainException>(() =>
            new SubscriptionPlan("BAD", "Bad", "", 0, 0, 0, 0, 1, 100, 30, true, true, true, 1));
    }

    [Fact]
    public void TenantUsageSnapshot_GetMonthlyPeriod_ShouldAlignToUtcMonthBounds()
    {
        var sample = new DateTimeOffset(2026, 11, 15, 14, 30, 0, TimeSpan.FromHours(6));
        var (start, end) = TenantUsageSnapshot.GetMonthlyPeriod(sample);

        start.ShouldBe(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        end.ShouldBe(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));
    }
}
