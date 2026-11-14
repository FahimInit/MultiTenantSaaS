using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;
using SaaS.Domain.Tenants;
using SaaS.Infrastructure.Identity;

namespace SaaS.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedDemoDataAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var enabled = config.GetValue<bool>("SeedDemoData:Enabled");
        if (!enabled || env.IsProduction())
        {
            return;
        }

        var demoPassword = config["SeedDemoData:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        if (await dbContext.Tenants.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        var now = dateTimeProvider.UtcNow;

        async Task<ApplicationUser> EnsureUserAsync(string email, string fullName, string? platformRole = null)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null)
            {
                return existing;
            }

            var user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true,
                PlatformRole = platformRole,
                CreatedAtUtc = now
            };

            var result = await userManager.CreateAsync(user, demoPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            return user;
        }

        _ = await EnsureUserAsync("platform.admin@saas.local", "Platform Operator", SystemRoleCodes.PlatformAdministrator);

        var alice = await EnsureUserAsync("alice@acme.local", "Alice Mercer");
        var bob = await EnsureUserAsync("bob@acme.local", "Bob Carter");
        var carol = await EnsureUserAsync("carol@acme.local", "Carol Lin");

        var dave = await EnsureUserAsync("dave@globex.local", "Dave Vance");
        var eve = await EnsureUserAsync("eve@globex.local", "Eve Morales");

        var consultant = await EnsureUserAsync("consultant@demo.local", "Jordan Cross");

        // Tenant 1: Acme Corp
        var acme = Tenant.Create("Acme Corp", "acme-corp", alice.Id, now);
        acme.Activate(now);
        acme.CompleteOnboarding(now);
        var acmeSettings = new TenantSettings(acme.Id, "billing@acme.local", now, "support@acme.local");
        var acmeRoles = TenantRole.CreateSystemRolesForTenant(acme.Id, now);

        var acmeOwner = acmeRoles.First(r => r.Code == SystemRoleCodes.OrganizationOwner);
        var acmeAdmin = acmeRoles.First(r => r.Code == SystemRoleCodes.OrganizationAdministrator);
        var acmeManager = acmeRoles.First(r => r.Code == SystemRoleCodes.Manager);
        var acmeMember = acmeRoles.First(r => r.Code == SystemRoleCodes.Member);

        // Tenant 2: Globex Labs
        var globex = Tenant.Create("Globex Labs", "globex-labs", dave.Id, now);
        globex.Activate(now);
        globex.CompleteOnboarding(now);
        var globexSettings = new TenantSettings(globex.Id, "billing@globex.local", now, "help@globex.local");
        var globexRoles = TenantRole.CreateSystemRolesForTenant(globex.Id, now);

        var globexOwner = globexRoles.First(r => r.Code == SystemRoleCodes.OrganizationOwner);
        var globexManager = globexRoles.First(r => r.Code == SystemRoleCodes.Manager);
        var globexMember = globexRoles.First(r => r.Code == SystemRoleCodes.Member);

        await dbContext.Tenants.AddRangeAsync([acme, globex], cancellationToken);
        await dbContext.TenantSettings.AddRangeAsync([acmeSettings, globexSettings], cancellationToken);
        await dbContext.TenantRoles.AddRangeAsync(acmeRoles, cancellationToken);
        await dbContext.TenantRoles.AddRangeAsync(globexRoles, cancellationToken);

        await dbContext.TenantMemberships.AddRangeAsync(
            [
                new TenantMembership(acme.Id, alice.Id, acmeOwner.Id, now),
                new TenantMembership(acme.Id, bob.Id, acmeAdmin.Id, now, alice.Id),
                new TenantMembership(acme.Id, carol.Id, acmeMember.Id, now, bob.Id),
                new TenantMembership(acme.Id, consultant.Id, acmeManager.Id, now, alice.Id),

                new TenantMembership(globex.Id, dave.Id, globexOwner.Id, now),
                new TenantMembership(globex.Id, eve.Id, globexManager.Id, now, dave.Id),
                new TenantMembership(globex.Id, consultant.Id, globexMember.Id, now, dave.Id)
            ],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
