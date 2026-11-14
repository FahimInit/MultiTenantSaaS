using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace SaaS.API.IntegrationTests.Infrastructure;

public sealed class PostgresWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "DemoPass123!";

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("saas_platform_test")
        .WithUsername("saas_test")
        .WithPassword("saas_test_pw_local")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgresContainer.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", "https://api.saas.test");
        builder.UseSetting("Jwt:Audience", "https://app.saas.test");
        builder.UseSetting("Jwt:SigningKey", "integration-test-only-signing-key-min-32-chars");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenDays", "14");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("SeedDemoData:Enabled", "true");
        builder.UseSetting("SeedDemoData:RunOnStartup", "false");
        builder.UseSetting("SeedDemoData:DefaultPassword", DemoPassword);
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
        await DatabaseSeeder.SeedDemoDataAsync(Services);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }
}
