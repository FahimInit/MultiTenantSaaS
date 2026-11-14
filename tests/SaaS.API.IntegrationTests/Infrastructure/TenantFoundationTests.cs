using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Common;
using SaaS.Infrastructure.MultiTenancy;
using SaaS.Infrastructure.Persistence.Interceptors;
using Shouldly;

namespace SaaS.API.IntegrationTests.Infrastructure;

public sealed class TenantFoundationTests
{
    private sealed class FakeTenantEntity : TenantEntity
    {
        public FakeTenantEntity(Guid tenantId) : base(tenantId) { }
    }

    private sealed class DummyDbContext : DbContext
    {
        public DummyDbContext(DbContextOptions<DummyDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FakeTenantEntity>();
        }
    }

    [Fact]
    public void TenantContext_ShouldStartUnresolvedAndFailClosed()
    {
        var context = new TenantContext();

        context.IsResolved.ShouldBeFalse();
        context.TenantId.ShouldBeNull();
        context.HasPermission("members.read").ShouldBeFalse();
    }

    [Fact]
    public void TenantContext_WhenSetAndCleared_ShouldTrackPermissionsCaseInsensitively()
    {
        var context = new TenantContext();
        var tenantId = Guid.CreateVersion7();
        var membershipId = Guid.CreateVersion7();

        context.SetTenant(tenantId, membershipId, "ORG_ADMIN", ["members.read", "members.invite"]);

        context.IsResolved.ShouldBeTrue();
        context.TenantId.ShouldBe(tenantId);
        context.MembershipId.ShouldBe(membershipId);
        context.RoleCode.ShouldBe("ORG_ADMIN");
        context.HasPermission("MEMBERS.READ").ShouldBeTrue();
        context.HasPermission("billing.manage").ShouldBeFalse();

        context.Clear();
        context.IsResolved.ShouldBeFalse();
        context.TenantId.ShouldBeNull();
        context.HasPermission("members.read").ShouldBeFalse();
    }

    [Fact]
    public async Task TenantWriteGuardInterceptor_WhenWritingForeignTenantEntity_ShouldThrowForbiddenAppException()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantA, Guid.CreateVersion7(), "ORG_ADMIN", ["members.invite"]);

        var interceptor = new TenantWriteGuardInterceptor(tenantContext);
        var options = new DbContextOptionsBuilder<DummyDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new DummyDbContext(options);
        db.Attach(new FakeTenantEntity(tenantB)).State = EntityState.Added;

        await Should.ThrowAsync<ForbiddenAppException>(() => db.SaveChangesAsync());
    }
}
