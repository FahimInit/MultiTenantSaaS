using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaaS.Application.Common.Abstractions;
using SaaS.Infrastructure.Audit;
using SaaS.Infrastructure.BackgroundJobs;
using SaaS.Infrastructure.Billing;
using SaaS.Infrastructure.Caching;
using SaaS.Infrastructure.Email;
using SaaS.Infrastructure.Identity;
using SaaS.Infrastructure.MultiTenancy;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Persistence.Interceptors;
using SaaS.Infrastructure.Persistence.Stores;
using SaaS.Infrastructure.Time;
using SaaS.Infrastructure.Usage;

namespace SaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddDataProtection();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IRequestContextInfo, HttpRequestContextInfo>();

        services.AddScoped<TenantWriteGuardInterceptor>();
        services.AddScoped<TenantCacheInvalidationInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var resolvedConfig = sp.GetRequiredService<IConfiguration>();
            var connectionString = resolvedConfig.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is not configured.");

            var writeGuard = sp.GetRequiredService<TenantWriteGuardInterceptor>();
            var cacheInvalidation = sp.GetRequiredService<TenantCacheInvalidationInterceptor>();

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(writeGuard, cacheInvalidation);
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IOpaqueTokenService, OpaqueTokenService>();
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<ITenantStore, TenantStore>();
        services.AddScoped<IMembershipStore, MembershipStore>();
        services.AddScoped<IInvitationStore, InvitationStore>();
        services.AddScoped<IRoleStore, RoleStore>();
        services.AddScoped<ISubscriptionStore, SubscriptionStore>();
        services.AddScoped<IAuditQueryStore, AuditQueryStore>();
        services.AddScoped<IDashboardStore, DashboardStore>();
        services.AddScoped<IPlatformAdminStore, PlatformAdminStore>();
        services.AddScoped<IPaymentProvider, MockPaymentProvider>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        var redisConnectionString = configuration["Redis:ConnectionString"]
            ?? configuration.GetConnectionString("Redis");

        if (redisEnabled && !string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = configuration["Redis:InstanceName"] ?? "saas:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<DistributedCacheService>();
        services.AddSingleton<ICacheService>(sp => sp.GetRequiredService<DistributedCacheService>());
        services.AddSingleton<ITenantPermissionCacheInvalidator>(sp => sp.GetRequiredService<DistributedCacheService>());

        services.AddScoped<TenantMembershipResolver>();
        services.AddScoped<ITenantMembershipResolver, CachedTenantMembershipResolver>();

        services.AddSingleton<ITenantUsageTracker, BufferedTenantUsageTracker>();
        services.AddSingleton<IPlatformMaintenanceRunner, PlatformMaintenanceRunner>();
        services.AddHostedService<PlatformMaintenanceHostedService>();

        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddSingleton<DevelopmentEmailService>();
        services.AddSingleton<IDevEmailOutbox>(sp => sp.GetRequiredService<DevelopmentEmailService>());
        if (string.Equals(configuration["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<DevelopmentEmailService>());
        }

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "postgresql",
                tags: ["ready"])
            .AddCheck<DistributedCacheHealthCheck>(
                name: "distributed_cache",
                tags: ["ready"]);

        return services;
    }
}
