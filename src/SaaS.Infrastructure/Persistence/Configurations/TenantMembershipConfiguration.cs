using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;
using SaaS.Domain.Tenants;
using SaaS.Infrastructure.Identity;

namespace SaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.InvitedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Composite FK guarantees a membership in Tenant A can NEVER reference a Role owned by Tenant B.
        builder.HasOne<TenantRole>()
            .WithMany()
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .HasForeignKey(m => new { m.TenantId, m.RoleId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.Property<uint>("xmin")
            .IsRowVersion();
    }
}
