using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;

namespace SaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("tenant_subscriptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.BillingCycle)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ExternalProviderSubscriptionId).HasMaxLength(128);

        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantSubscription>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TenantId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.TrialEndsAtUtc });

        builder.Property<uint>("xmin")
            .IsRowVersion();
    }
}
