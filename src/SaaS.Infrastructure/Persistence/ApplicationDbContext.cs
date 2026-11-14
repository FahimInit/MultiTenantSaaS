using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Audit;
using SaaS.Domain.Common;
using SaaS.Domain.Identity;
using SaaS.Domain.Memberships;
using SaaS.Domain.Tenants;
using SaaS.Infrastructure.Identity;

namespace SaaS.Infrastructure.Persistence;

public sealed class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private static readonly MethodInfo ApplyTenantFilterMethod = typeof(ApplicationDbContext)
        .GetMethod(nameof(ApplyTenantQueryFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentityTableNames(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ApplyGlobalTenantFilters(builder);

        // Fail-closed filter on AuditLog for tenant-scoped queries; platform audit queries explicitly call IgnoreQueryFilters().
        builder.Entity<AuditLog>()
            .HasQueryFilter(a =>
                _tenantContext.TenantId != null &&
                a.Scope == AuditScope.Tenant &&
                a.TenantId == _tenantContext.TenantId);
    }

    private static void ConfigureIdentityTableNames(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PlatformRole).HasMaxLength(64);
        });

        builder.Entity<IdentityRole<Guid>>().ToTable("identity_roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("identity_user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("identity_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("identity_user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("identity_role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("identity_user_tokens");
    }

    private void ApplyGlobalTenantFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [builder]);
            }
        }
    }

    private void ApplyTenantQueryFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantScoped
    {
        // Fail-closed: returns zero rows if ITenantContext is unresolved unless IgnoreQueryFilters() is explicitly invoked.
        builder.Entity<TEntity>()
            .HasQueryFilter(e => _tenantContext.TenantId != null && e.TenantId == _tenantContext.TenantId);
    }
}
