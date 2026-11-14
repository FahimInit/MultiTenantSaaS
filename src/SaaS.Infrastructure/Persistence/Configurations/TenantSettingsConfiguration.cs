using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Tenants;

namespace SaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("tenant_settings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BillingEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SupportEmail).HasMaxLength(256);
        builder.Property(x => x.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Locale).HasMaxLength(24).IsRequired();

        var domainsComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            c => c.Aggregate(0, (acc, v) => HashCode.Combine(acc, v.GetHashCode(StringComparison.OrdinalIgnoreCase))),
            c => c.ToList());

        builder.Property(x => x.AllowedEmailDomains)
            .HasColumnName("allowed_domains_json")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(domainsComparer);

        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantSettings>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}
