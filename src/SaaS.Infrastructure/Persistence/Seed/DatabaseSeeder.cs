using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Audit;
using SaaS.Domain.Memberships;
using SaaS.Domain.Subscriptions;
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

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        var plans = await EnsureSubscriptionPlansAsync(dbContext, cancellationToken);

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

        // Tenant 1: Acme Corp (Pro Active)
        var acme = Tenant.Create("Acme Corp", "acme-corp", alice.Id, now);
        acme.Activate(now);
        acme.CompleteOnboarding(now);
        var acmeSettings = new TenantSettings(acme.Id, "billing@acme.local", now, "support@acme.local");
        var acmeRoles = TenantRole.CreateSystemRolesForTenant(acme.Id, now);

        var acmeOwner = acmeRoles.First(r => r.Code == SystemRoleCodes.OrganizationOwner);
        var acmeAdmin = acmeRoles.First(r => r.Code == SystemRoleCodes.OrganizationAdministrator);
        var acmeManager = acmeRoles.First(r => r.Code == SystemRoleCodes.Manager);
        var acmeMember = acmeRoles.First(r => r.Code == SystemRoleCodes.Member);

        // Tenant 2: Globex Labs (Free Active — 3/3 seats occupied to demonstrate quota & plan gates)
        var globex = Tenant.Create("Globex Labs", "globex-labs", dave.Id, now);
        globex.Activate(now);
        globex.CompleteOnboarding(now);
        var globexSettings = new TenantSettings(globex.Id, "billing@globex.local", now, "help@globex.local");
        var globexRoles = TenantRole.CreateSystemRolesForTenant(globex.Id, now);

        var globexOwner = globexRoles.First(r => r.Code == SystemRoleCodes.OrganizationOwner);
        var globexManager = globexRoles.First(r => r.Code == SystemRoleCodes.Manager);
        var globexMember = globexRoles.First(r => r.Code == SystemRoleCodes.Member);

        var proPlan = plans["PRO"];
        var freePlan = plans["FREE"];

        var acmeSub = TenantSubscription.CreateActive(
            acme.Id,
            proPlan.Id,
            BillingCycle.Monthly,
            "mock_sub_acme_demo",
            now);

        var globexSub = TenantSubscription.CreateActive(
            globex.Id,
            freePlan.Id,
            BillingCycle.Monthly,
            externalProviderSubscriptionId: null,
            utcNow: now,
            autoRenew: false);

        var acmeHistory = new SubscriptionHistory(
            acme.Id,
            acmeSub.Id,
            null,
            proPlan.Id,
            SubscriptionEventType.Subscribed,
            proPlan.MonthlyPriceCents,
            "USD",
            "mock_pay_acme_init",
            alice.Id,
            "Subscribed to Pro (Monthly) plan.",
            now);

        var globexHistory = new SubscriptionHistory(
            globex.Id,
            globexSub.Id,
            null,
            freePlan.Id,
            SubscriptionEventType.Subscribed,
            0,
            "USD",
            "FREE_TIER",
            dave.Id,
            "Provisioned Free tier.",
            now);

        var (periodStart, periodEnd) = TenantUsageSnapshot.GetMonthlyPeriod(now);
        var acmeUsage = new TenantUsageSnapshot(acme.Id, periodStart, periodEnd, 4, 0, 128, now);
        var globexUsage = new TenantUsageSnapshot(globex.Id, periodStart, periodEnd, 3, 0, 42, now);

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

        await dbContext.TenantSubscriptions.AddRangeAsync([acmeSub, globexSub], cancellationToken);
        await dbContext.SubscriptionHistory.AddRangeAsync([acmeHistory, globexHistory], cancellationToken);
        await dbContext.TenantUsageSnapshots.AddRangeAsync([acmeUsage, globexUsage], cancellationToken);

        await dbContext.AuditLogs.AddRangeAsync(
            [
                AuditLog.CreateTenantEvent(
                    acme.Id, alice.Id, alice.Email!, AuditCategory.Tenant, "TENANT_CREATED", "Tenant", acme.Id.ToString(),
                    "Organization 'Acme Corp' initialized.", now.AddMinutes(-15)),
                AuditLog.CreateTenantEvent(
                    acme.Id, alice.Id, alice.Email!, AuditCategory.Subscription, "SUBSCRIPTION_PLAN_CHANGED", "TenantSubscription", acmeSub.Id.ToString(),
                    "Activated Pro Monthly subscription plan.", now.AddMinutes(-10)),
                AuditLog.CreateTenantEvent(
                    globex.Id, dave.Id, dave.Email!, AuditCategory.Tenant, "TENANT_CREATED", "Tenant", globex.Id.ToString(),
                    "Organization 'Globex Labs' initialized.", now.AddMinutes(-12))
            ],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static async Task<Dictionary<string, SubscriptionPlan>> EnsureSubscriptionPlansAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.SubscriptionPlans
            .ToDictionaryAsync(p => p.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var defaults = new[]
        {
            new SubscriptionPlan(
                code: "FREE",
                name: "Free",
                description: "Starter tier for small teams getting started with multi-tenant workspace management.",
                monthlyPriceCents: 0,
                annualPriceCents: 0,
                trialDays: 0,
                maxMembers: 3,
                maxCustomRoles: 0,
                maxMonthlyApiCalls: 5_000,
                auditRetentionDays: 30,
                allowCustomRoles: false,
                isActive: true,
                isPublic: true,
                sortOrder: 1),
            new SubscriptionPlan(
                code: "PRO",
                name: "Pro",
                description: "Growth plan unlocking custom RBAC roles, extended audit retention, and 25 workspace seats.",
                monthlyPriceCents: 4900,
                annualPriceCents: 49000,
                trialDays: 14,
                maxMembers: 25,
                maxCustomRoles: 10,
                maxMonthlyApiCalls: 100_000,
                auditRetentionDays: 180,
                allowCustomRoles: true,
                isActive: true,
                isPublic: true,
                sortOrder: 2),
            new SubscriptionPlan(
                code: "ENTERPRISE",
                name: "Enterprise",
                description: "Scale-grade multi-tenant tier with unlimited seats, unlimited custom roles, and 1-year audit compliance.",
                monthlyPriceCents: 19900,
                annualPriceCents: 199000,
                trialDays: 14,
                maxMembers: null,
                maxCustomRoles: null,
                maxMonthlyApiCalls: 2_000_000,
                auditRetentionDays: 365,
                allowCustomRoles: true,
                isActive: true,
                isPublic: true,
                sortOrder: 3)
        };

        var addedAny = false;
        foreach (var plan in defaults)
        {
            if (!existing.ContainsKey(plan.Code))
            {
                await dbContext.SubscriptionPlans.AddAsync(plan, cancellationToken);
                existing[plan.Code] = plan;
                addedAny = true;
            }
        }

        if (addedAny)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return existing;
    }
}
