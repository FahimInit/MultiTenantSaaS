using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Tenants;

namespace SaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantRoleConfiguration : IEntityTypeConfiguration<TenantRole>
{
    public void Configure(EntityTypeBuilder<TenantRole> builder)
    {
        builder.ToTable("tenant_roles");
        builder.HasKey(x => x.Id);

        // Alternate composite key enabling PostgreSQL composite FKs from memberships and permissions.
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });

        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(400).IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Permissions)
            .WithOne()
            .HasPrincipalKey(r => new { r.TenantId, r.Id })
            .HasForeignKey(p => new { p.TenantId, p.RoleId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Permissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();

        builder.Property<uint>("xmin")
            .IsRowVersion();
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("tenant_role_permissions");
        builder.HasKey(x => new { x.TenantId, x.RoleId, x.PermissionCode });

        builder.Property(x => x.PermissionCode).HasMaxLength(100).IsRequired();
    }
}
