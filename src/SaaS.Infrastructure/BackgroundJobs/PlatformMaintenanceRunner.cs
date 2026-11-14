using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.Audit;
using SaaS.Domain.Invitations;
using SaaS.Domain.Subscriptions;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.BackgroundJobs;

public sealed class PlatformMaintenanceRunner : IPlatformMaintenanceRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantUsageTracker _usageTracker;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<PlatformMaintenanceRunner> _logger;

    public PlatformMaintenanceRunner(
        IServiceScopeFactory scopeFactory,
        ITenantUsageTracker usageTracker,
        IDateTimeProvider dateTimeProvider,
        ILogger<PlatformMaintenanceRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _usageTracker = usageTracker;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<MaintenanceRunResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;

        await _usageTracker.FlushPendingUsageAsync(cancellationToken);

        var expiredInvitesCount = await ExpireOverdueInvitationsAsync(now, cancellationToken);
        var expiredTrialsCount = await ExpireOverdueTrialsAsync(now, cancellationToken);
        var purgedTokensCount = await PurgeOldRefreshTokensAsync(now, cancellationToken);

        if (expiredInvitesCount > 0 || expiredTrialsCount > 0 || purgedTokensCount > 0)
        {
            _logger.LogInformation(
                "Maintenance run completed: ExpiredInvitations={Invites}, ExpiredTrials={Trials}, PurgedRefreshTokens={Tokens}",
                expiredInvitesCount,
                expiredTrialsCount,
                purgedTokensCount);
        }

        return new MaintenanceRunResult(expiredInvitesCount, expiredTrialsCount, 1, purgedTokensCount);
    }

    private async Task<int> ExpireOverdueInvitationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var discoveryScope = _scopeFactory.CreateScope();
        var discoveryDb = discoveryScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var overdueTenantIds = await discoveryDb.TenantInvitations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAtUtc <= now)
            .Select(i => i.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var totalExpired = 0;

        foreach (var tenantId in overdueTenantIds)
        {
            // Isolate each tenant inside its own DI scope + verified ITenantContextSetter boundary.
            using var tenantScope = _scopeFactory.CreateScope();
            var tenantSetter = tenantScope.ServiceProvider.GetRequiredService<ITenantContextSetter>();
            var db = tenantScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            tenantSetter.SetSystemJobTenant(tenantId);
            try
            {
                var overdue = await db.TenantInvitations
                    .Where(i => i.TenantId == tenantId && i.Status == InvitationStatus.Pending && i.ExpiresAtUtc <= now)
                    .ToListAsync(cancellationToken);

                foreach (var invite in overdue)
                {
                    invite.MarkExpired();
                    await db.AuditLogs.AddAsync(
                        AuditLog.CreateTenantEvent(
                            tenantId,
                            null,
                            "system@maintenance",
                            AuditCategory.Membership,
                            "INVITATION_EXPIRED",
                            "TenantInvitation",
                            invite.Id.ToString(),
                            $"Invitation for '{invite.Email}' expired automatically.",
                            now),
                        cancellationToken);
                    totalExpired++;
                }

                await db.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                tenantSetter.Clear();
            }
        }

        return totalExpired;
    }

    private async Task<int> ExpireOverdueTrialsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var discoveryScope = _scopeFactory.CreateScope();
        var discoveryDb = discoveryScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var overdueTenantIds = await discoveryDb.TenantSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Trialing &&
                        s.TrialEndsAtUtc.HasValue &&
                        s.TrialEndsAtUtc.Value <= now)
            .Select(s => s.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var totalExpired = 0;

        foreach (var tenantId in overdueTenantIds)
        {
            using var tenantScope = _scopeFactory.CreateScope();
            var tenantSetter = tenantScope.ServiceProvider.GetRequiredService<ITenantContextSetter>();
            var db = tenantScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            tenantSetter.SetSystemJobTenant(tenantId);
            try
            {
                var subscription = await db.TenantSubscriptions
                    .FirstOrDefaultAsync(
                        s => s.TenantId == tenantId &&
                             s.Status == SubscriptionStatus.Trialing &&
                             s.TrialEndsAtUtc.HasValue &&
                             s.TrialEndsAtUtc.Value <= now,
                        cancellationToken);

                if (subscription is null)
                {
                    continue;
                }

                subscription.MarkExpired(now);

                await db.SubscriptionHistory.AddAsync(
                    new SubscriptionHistory(
                        tenantId,
                        subscription.Id,
                        subscription.PlanId,
                        subscription.PlanId,
                        SubscriptionEventType.Expired,
                        0,
                        "USD",
                        null,
                        null,
                        "Subscription trial period expired automatically.",
                        now),
                    cancellationToken);

                await db.AuditLogs.AddAsync(
                    AuditLog.CreateTenantEvent(
                        tenantId,
                        null,
                        "system@maintenance",
                        AuditCategory.Subscription,
                        "TRIAL_EXPIRED",
                        "TenantSubscription",
                        subscription.Id.ToString(),
                        "Organization trial period expired automatically.",
                        now),
                    cancellationToken);

                await db.SaveChangesAsync(cancellationToken);
                totalExpired++;
            }
            finally
            {
                tenantSetter.Clear();
            }
        }

        return totalExpired;
    }

    private async Task<int> PurgeOldRefreshTokensAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cutoff = now.AddDays(-30);

        return await db.RefreshTokens
            .Where(t => (t.RevokedAtUtc.HasValue && t.RevokedAtUtc.Value < cutoff) || t.ExpiresAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}

public sealed class PlatformMaintenanceHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IPlatformMaintenanceRunner _runner;
    private readonly ITenantUsageTracker _usageTracker;
    private readonly ILogger<PlatformMaintenanceHostedService> _logger;

    public PlatformMaintenanceHostedService(
        IPlatformMaintenanceRunner runner,
        ITenantUsageTracker usageTracker,
        ILogger<PlatformMaintenanceHostedService> logger)
    {
        _runner = runner;
        _usageTracker = usageTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _runner.RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled failure during scheduled maintenance run.");
                }
            }
        }
        finally
        {
            using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _usageTracker.FlushPendingUsageAsync(shutdownCts.Token);
        }
    }
}
