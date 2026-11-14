using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;

namespace SaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantUsageSnapshotConfiguration : IEntityTypeConfiguration<TenantUsageSnapshot>
{
    public void Configure(EntityTypeBuilder<TenantUsageSnapshot> builder)
    {
        builder.ToTable("tenant_usage_snapshots");
        builder.HasKey(x => x.Id);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.PeriodStartUtc }).IsUnique();
    }
}
