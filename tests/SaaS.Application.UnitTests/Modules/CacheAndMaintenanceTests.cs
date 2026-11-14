using NSubstitute;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Modules.Memberships;
using SaaS.Application.Modules.PlatformAdmin;
using SaaS.Application.Modules.Roles;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;
using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;
using Shouldly;

namespace SaaS.Application.UnitTests.Modules;

public sealed class CacheAndMaintenanceTests
{
    [Fact]
    public async Task UpdateMemberRoleHandler_WhenRoleChanges_ShouldInvalidateTargetUserCache()
    {
        var tenantId = Guid.CreateVersion7();
        var targetUserId = Guid.CreateVersion7();
        var actorUserId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var roles = TenantRole.CreateSystemRolesForTenant(tenantId, now);
        var memberRole = roles.First(r => r.Code == SystemRoleCodes.Member);
        var managerRole = roles.First(r => r.Code == SystemRoleCodes.Manager);

        var targetMembership = new TenantMembership(tenantId, targetUserId, memberRole.Id, now);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.RoleCode.Returns(SystemRoleCodes.OrganizationOwner);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(actorUserId);
        currentUser.Email.Returns("owner@acme.local");

        var membershipStore = Substitute.For<IMembershipStore>();
        membershipStore.FindMemberDetailAsync(tenantId, targetMembership.Id, Arg.Any<CancellationToken>())
            .Returns(new MemberDetailWithRole(
                targetMembership,
                "carol@acme.local",
                "Carol Lin",
                SystemRoleCodes.Member,
                "Member"));

        var roleStore = Substitute.For<IRoleStore>();
        roleStore.FindByIdWithPermissionsAsync(tenantId, managerRole.Id, Arg.Any<CancellationToken>())
            .Returns(managerRole);

        var cacheInvalidator = Substitute.For<ITenantPermissionCacheInvalidator>();
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new UpdateMemberRoleHandler(
            new UpdateMemberRoleValidator(),
            tenantContext,
            currentUser,
            membershipStore,
            roleStore,
            Substitute.For<ISubscriptionQuotaService>(),
            cacheInvalidator,
            Substitute.For<IUnitOfWork>(),
            dateTimeProvider,
            Substitute.For<IAuditLogger>());

        var result = await handler.HandleAsync(new UpdateMemberRoleCommand(targetMembership.Id, managerRole.Id));

        result.RoleCode.ShouldBe(SystemRoleCodes.Manager);
        await cacheInvalidator.Received(1)
            .InvalidateUserTenantAsync(tenantId, targetUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateRoleHandler_WhenPermissionsUpdated_ShouldRotateTenantAuthzEpoch()
    {
        var tenantId = Guid.CreateVersion7();
        var actorUserId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var customRole = TenantRole.CreateCustom(
            tenantId,
            "Billing Viewer",
            "Read billing and dashboard",
            [PermissionCatalog.DashboardRead, PermissionCatalog.BillingRead],
            now);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);
        tenantContext.RoleCode.Returns(SystemRoleCodes.OrganizationOwner);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(actorUserId);
        currentUser.Email.Returns("owner@acme.local");

        var roleStore = Substitute.For<IRoleStore>();
        roleStore.FindByIdWithPermissionsAsync(tenantId, customRole.Id, Arg.Any<CancellationToken>())
            .Returns(customRole);
        roleStore.RoleNameExistsAsync(tenantId, "Billing Ops", customRole.Id, Arg.Any<CancellationToken>())
            .Returns(false);
        roleStore.CountRoleReferencesAsync(tenantId, customRole.Id, Arg.Any<CancellationToken>())
            .Returns(2);

        var cacheInvalidator = Substitute.For<ITenantPermissionCacheInvalidator>();
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var handler = new UpdateRoleHandler(
            new UpdateRoleValidator(),
            tenantContext,
            currentUser,
            roleStore,
            Substitute.For<ISubscriptionQuotaService>(),
            cacheInvalidator,
            Substitute.For<IUnitOfWork>(),
            dateTimeProvider,
            Substitute.For<IAuditLogger>());

        var updated = await handler.HandleAsync(new UpdateRoleCommand(
            customRole.Id,
            "Billing Ops",
            "Updated role",
            [PermissionCatalog.DashboardRead]));

        updated.Name.ShouldBe("Billing Ops");
        await cacheInvalidator.Received(1)
            .InvalidateTenantMembersAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuspendAndReactivateTenantHandlers_ShouldRotateTenantAuthzEpochImmediately()
    {
        var ownerId = Guid.CreateVersion7();
        var platformAdminId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var tenant = Tenant.Create("Acme Corp", "acme-corp", ownerId, now);
        tenant.Activate(now);

        var platformAdminStore = Substitute.For<IPlatformAdminStore>();
        platformAdminStore.FindTenantForUpdateAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        platformAdminStore.GetTenantDetailAsync(tenant.Id, now, Arg.Any<CancellationToken>())
            .Returns(_ => new PlatformTenantDetailDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug.Value,
                tenant.Status,
                tenant.SuspensionReason,
                tenant.OnboardingCompletedAtUtc.HasValue,
                tenant.OnboardingCompletedAtUtc,
                tenant.CreatedAtUtc,
                3,
                0,
                0,
                0,
                "PRO",
                "Pro",
                SubscriptionStatus.Active,
                BillingCycle.Monthly,
                now.AddMonths(1),
                null,
                15));

        var cacheInvalidator = Substitute.For<ITenantPermissionCacheInvalidator>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(platformAdminId);
        currentUser.Email.Returns("platform.admin@saas.local");
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);

        var suspendHandler = new SuspendTenantHandler(
            new SuspendTenantValidator(),
            platformAdminStore,
            cacheInvalidator,
            currentUser,
            Substitute.For<IUnitOfWork>(),
            dateTimeProvider,
            Substitute.For<IAuditLogger>());

        var suspended = await suspendHandler.HandleAsync(new SuspendTenantCommand(tenant.Id, "Chargeback audit"));
        suspended.Status.ShouldBe(TenantStatus.Suspended);

        var reactivateHandler = new ReactivateTenantHandler(
            platformAdminStore,
            cacheInvalidator,
            currentUser,
            Substitute.For<IUnitOfWork>(),
            dateTimeProvider,
            Substitute.For<IAuditLogger>());

        var reactivated = await reactivateHandler.HandleAsync(new ReactivateTenantCommand(tenant.Id));
        reactivated.Status.ShouldBe(TenantStatus.Active);

        await cacheInvalidator.Received(2)
            .InvalidateTenantMembersAsync(tenant.Id, Arg.Any<CancellationToken>());
    }
}
