using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Subscriptions;
using SaaS.Domain.Tenants;

namespace SaaS.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionHistoryConfiguration : IEntityTypeConfiguration<SubscriptionHistory>
{
    public void Configure(EntityTypeBuilder<SubscriptionHistory> builder)
    {
        builder.ToTable("subscription_history");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.PaymentReference).HasMaxLength(128);
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TenantSubscription>()
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(x => x.FromPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(x => x.ToPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.OccurredAtUtc })
            .IsDescending(false, true);
    }
}
