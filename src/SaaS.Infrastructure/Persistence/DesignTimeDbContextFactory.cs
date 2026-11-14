using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SaaS.Infrastructure.MultiTenancy;

namespace SaaS.Infrastructure.Persistence;

/// <summary>
/// Enables EF Core CLI tooling (migrations) without relying on runtime secrets or appsettings passwords.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=saas_platform_design;Username=saas_design;Password=design_only_not_used_at_runtime";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();

        return new ApplicationDbContext(optionsBuilder.Options, new TenantContext());
    }
}
