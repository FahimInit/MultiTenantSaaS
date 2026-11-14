using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Invitations;
using SaaS.Domain.Tenants;
using SaaS.Infrastructure.Identity;

namespace SaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.ToTable("tenant_invitations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite FK ensures an invitation inside Tenant A can never reference a role from Tenant B.
        builder.HasOne<TenantRole>()
            .WithMany()
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .HasForeignKey(i => new { i.TenantId, i.RoleId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.AcceptedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.NormalizedEmail })
            .IsUnique()
            .HasFilter("status = 'Pending'");
        builder.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.CreatedAtUtc })
            .IsDescending(false, true);
    }
}
