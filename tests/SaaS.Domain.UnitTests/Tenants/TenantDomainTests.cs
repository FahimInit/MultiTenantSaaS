using SaaS.Domain.AccessControl;
using SaaS.Domain.Common;
using SaaS.Domain.Identity;
using SaaS.Domain.Tenants;
using Shouldly;

namespace SaaS.Domain.UnitTests.Tenants;

public sealed class TenantDomainTests
{
    [Theory]
    [InlineData("Acme-Corp", "acme-corp")]
    [InlineData("  globex123  ", "globex123")]
    public void TenantSlug_ValidInput_ShouldNormalize(string raw, string expected)
    {
        var slug = TenantSlug.Create(raw);
        slug.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("-bad-start")]
    [InlineData("bad-end-")]
    [InlineData("bad--hyphen")]
    [InlineData("bad slug")]
    [InlineData("admin")]
    [InlineData("platform")]
    public void TenantSlug_InvalidOrReserved_ShouldThrowDomainException(string raw)
    {
        Should.Throw<DomainException>(() => TenantSlug.Create(raw));
    }

    [Fact]
    public void Tenant_LifecycleTransitions_ShouldEnforceInvariants()
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Acme Corp", "acme-corp", Guid.CreateVersion7(), now);

        tenant.Status.ShouldBe(TenantStatus.Trial);
        tenant.IsOperational.ShouldBeTrue();
        tenant.DomainEvents.Count.ShouldBe(1);

        tenant.Suspend("Non-payment", now.AddMinutes(5));
        tenant.Status.ShouldBe(TenantStatus.Suspended);
        tenant.IsOperational.ShouldBeFalse();

        tenant.Activate(now.AddMinutes(10));
        tenant.Status.ShouldBe(TenantStatus.Active);
        tenant.SuspensionReason.ShouldBeNull();

        tenant.Deactivate(now.AddMinutes(20));
        Should.Throw<DomainException>(() => tenant.Activate(now.AddMinutes(25)));
    }

    [Fact]
    public void TenantSettings_WhenEnforceDomainWithoutDomains_ShouldThrowDomainException()
    {
        var settings = new TenantSettings(Guid.CreateVersion7(), "billing@acme.local", DateTimeOffset.UtcNow);

        Should.Throw<DomainException>(() => settings.Update(
            "billing@acme.local",
            null,
            "UTC",
            "en-US",
            enforceEmailDomain: true,
            allowedEmailDomains: [],
            sessionTimeoutMinutes: 60,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void TenantRole_SystemRolesCreationAndOwnerImmutability_ShouldWork()
    {
        var tenantId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var roles = TenantRole.CreateSystemRolesForTenant(tenantId, now);

        roles.Count.ShouldBe(4);
        var owner = roles.Single(r => r.Code == SystemRoleCodes.OrganizationOwner);
        owner.IsSystemRole.ShouldBeTrue();
        owner.Permissions.Count.ShouldBe(PermissionCatalog.AllTenantPermissions.Count);

        Should.Throw<DomainException>(() =>
            owner.UpdateDetails("Owner", "Modified", [PermissionCatalog.TenantRead], now));
    }

    [Fact]
    public void RefreshToken_StateTransitions_ShouldTrackActiveExpiredAndRevoked()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "HASH_ABC",
            now,
            now.AddDays(7));

        token.IsActive(now.AddHours(1)).ShouldBeTrue();
        token.IsExpired(now.AddDays(8)).ShouldBeTrue();
        token.IsActive(now.AddDays(8)).ShouldBeFalse();

        token.Revoke("Rotated", now.AddHours(2), Guid.CreateVersion7());
        token.IsRevoked.ShouldBeTrue();
        token.IsActive(now.AddHours(3)).ShouldBeFalse();
    }
}
