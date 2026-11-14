using NSubstitute;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Application.Modules.Memberships;
using SaaS.Application.Modules.Roles;
using SaaS.Application.Modules.Subscriptions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;
using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;
using Shouldly;

namespace SaaS.Application.UnitTests.Modules;

public sealed class BusinessModuleHandlerTests
{
    [Fact]
    public async Task CreateCustomRoleHandler_WhenNonOwnerGrantsUnheldPermission_ShouldPreventPrivilegeEscalation()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.RoleCode.Returns(SystemRoleCodes.OrganizationAdministrator);
        tenantContext.HasPermission(PermissionCatalog.RolesManage).Returns(true);
        tenantContext.HasPermission(PermissionCatalog.BillingManage).Returns(false);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.Email.Returns("admin@acme.local");

        var handler = new CreateCustomRoleHandler(
            new CreateCustomRoleValidator(),
            tenantContext,
            currentUser,
            Substitute.For<IRoleStore>(),
            Substitute.For<ISubscriptionQuotaService>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<IAuditLogger>());

        var ex = await Should.ThrowAsync<ForbiddenAppException>(() =>
            handler.HandleAsync(new CreateCustomRoleCommand(
                "Rogue Role",
                "Attempt privilege escalation",
                [PermissionCatalog.BillingManage])));

        ex.ErrorCode.ShouldBe("privilege_escalation_denied");
    }

    [Fact]
    public async Task UpdateMemberRoleHandler_WhenDemotingLastActiveOwner_ShouldThrowConflictAppException()
    {
        var tenantId = Guid.CreateVersion7();
        var ownerMemberId = Guid.CreateVersion7();
        var actorUserId = Guid.CreateVersion7();
        var ownerRoleId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var roles = TenantRole.CreateSystemRolesForTenant(tenantId, now);
        var adminRole = roles.First(r => r.Code == SystemRoleCodes.OrganizationAdministrator);

        var ownerMembership = new TenantMembership(tenantId, actorUserId, ownerRoleId, now);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.RoleCode.Returns(SystemRoleCodes.OrganizationOwner);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(actorUserId);
        currentUser.Email.Returns("alice@acme.local");

        var membershipStore = Substitute.For<IMembershipStore>();
        membershipStore.FindMemberDetailAsync(tenantId, ownerMemberId, Arg.Any<CancellationToken>())
            .Returns(new MemberDetailWithRole(
                ownerMembership,
                "alice@acme.local",
                "Alice",
                SystemRoleCodes.OrganizationOwner,
                "Organization Owner"));
        membershipStore.CountActiveOwnersAsync(tenantId, ownerMembership.Id, Arg.Any<CancellationToken>())
            .Returns(0);

        var roleStore = Substitute.For<IRoleStore>();
        roleStore.FindByIdWithPermissionsAsync(tenantId, adminRole.Id, Arg.Any<CancellationToken>())
            .Returns(adminRole);

        var handler = new UpdateMemberRoleHandler(
            new UpdateMemberRoleValidator(),
            tenantContext,
            currentUser,
            membershipStore,
            roleStore,
            Substitute.For<ISubscriptionQuotaService>(),
            Substitute.For<ITenantPermissionCacheInvalidator>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<IAuditLogger>());

        var ex = await Should.ThrowAsync<ConflictAppException>(() =>
            handler.HandleAsync(new UpdateMemberRoleCommand(ownerMemberId, adminRole.Id)));

        ex.ErrorCode.ShouldBe("last_owner_protected");
    }

    [Fact]
    public async Task InviteMemberHandler_WhenEmailDomainRestricted_ShouldThrowValidationAppException()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var tenant = Tenant.Create("Acme Corp", "acme-corp", userId, now);
        var settings = new TenantSettings(tenant.Id, "billing@acme.local", now);
        settings.Update("billing@acme.local", null, "UTC", "en-US", enforceEmailDomain: true, ["acme.local"], 1440, now);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenant.Id);
        tenantContext.RoleCode.Returns(SystemRoleCodes.OrganizationOwner);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.Email.Returns("alice@acme.local");

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.GetCurrentTenantAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        tenantStore.GetCurrentTenantSettingsAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(settings);

        var handler = new InviteMemberHandler(
            new InviteMemberValidator(),
            tenantContext,
            currentUser,
            tenantStore,
            Substitute.For<IRoleStore>(),
            Substitute.For<IMembershipStore>(),
            Substitute.For<IInvitationStore>(),
            Substitute.For<ISubscriptionQuotaService>(),
            Substitute.For<IOpaqueTokenService>(),
            Substitute.For<IEmailService>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<IAuditLogger>());

        await Should.ThrowAsync<ValidationAppException>(() =>
            handler.HandleAsync(new InviteMemberCommand("outsider@gmail.com", Guid.CreateVersion7())));
    }

    [Fact]
    public async Task ChangeSubscriptionPlanHandler_WhenDowngradeViolatesMemberCount_ShouldThrowConflict()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var tenant = Tenant.Create("Acme Corp", "acme-corp", userId, now);
        var proPlan = new SubscriptionPlan("PRO", "Pro", "", 4900, 49000, 14, 25, 10, 100000, 180, true, true, true, 2);
        var freePlan = new SubscriptionPlan("FREE", "Free", "", 0, 0, 0, 3, 0, 5000, 30, false, true, true, 1);
        var currentSub = TenantSubscription.CreateActive(tenant.Id, proPlan.Id, BillingCycle.Monthly, "sub_1", now);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenant.Id);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.Email.Returns("alice@acme.local");

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.GetCurrentTenantAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var subStore = Substitute.For<ISubscriptionStore>();
        subStore.GetTenantSubscriptionWithPlanAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(new TenantSubscriptionWithPlan(currentSub, proPlan));
        subStore.FindPlanByCodeAsync("FREE", Arg.Any<CancellationToken>()).Returns(freePlan);

        var membershipStore = Substitute.For<IMembershipStore>();
        membershipStore.CountActiveMembersAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(5);

        var inviteStore = Substitute.For<IInvitationStore>();
        inviteStore.CountPendingInvitationsAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(1);

        var handler = new ChangeSubscriptionPlanHandler(
            new ChangeSubscriptionPlanValidator(),
            tenantContext,
            currentUser,
            tenantStore,
            subStore,
            membershipStore,
            inviteStore,
            Substitute.For<IRoleStore>(),
            Substitute.For<IPaymentProvider>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<IAuditLogger>());

        var ex = await Should.ThrowAsync<ConflictAppException>(() =>
            handler.HandleAsync(new ChangeSubscriptionPlanCommand("FREE", BillingCycle.Monthly)));

        ex.ErrorCode.ShouldBe("downgrade_member_limit_conflict");
    }

    [Fact]
    public async Task ChangeSubscriptionPlanHandler_WhenPaymentDeclined_ShouldThrowForbidden()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var tenant = Tenant.Create("Acme Corp", "acme-corp", userId, now);
        var freePlan = new SubscriptionPlan("FREE", "Free", "", 0, 0, 0, 3, 0, 5000, 30, false, true, true, 1);
        var proPlan = new SubscriptionPlan("PRO", "Pro", "", 4900, 49000, 14, 25, 10, 100000, 180, true, true, true, 2);
        var currentSub = TenantSubscription.CreateActive(tenant.Id, freePlan.Id, BillingCycle.Monthly, null, now, false);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenant.Id);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.GetCurrentTenantAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var subStore = Substitute.For<ISubscriptionStore>();
        subStore.GetTenantSubscriptionWithPlanAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(new TenantSubscriptionWithPlan(currentSub, freePlan));
        subStore.FindPlanByCodeAsync("PRO", Arg.Any<CancellationToken>()).Returns(proPlan);

        var paymentProvider = Substitute.For<IPaymentProvider>();
        paymentProvider.ProcessSubscriptionPaymentAsync(Arg.Any<PaymentChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentChargeResult(false, null, "", "Card declined"));

        var handler = new ChangeSubscriptionPlanHandler(
            new ChangeSubscriptionPlanValidator(),
            tenantContext,
            currentUser,
            tenantStore,
            subStore,
            Substitute.For<IMembershipStore>(),
            Substitute.For<IInvitationStore>(),
            Substitute.For<IRoleStore>(),
            paymentProvider,
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IDateTimeProvider>(),
            Substitute.For<IAuditLogger>());

        var ex = await Should.ThrowAsync<ForbiddenAppException>(() =>
            handler.HandleAsync(new ChangeSubscriptionPlanCommand("PRO", BillingCycle.Monthly, "tok_declined")));

        ex.ErrorCode.ShouldBe("payment_failed");
    }
}
