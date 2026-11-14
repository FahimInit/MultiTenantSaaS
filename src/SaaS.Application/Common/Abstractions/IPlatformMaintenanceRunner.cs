namespace SaaS.Application.Common.Abstractions;

public sealed record MaintenanceRunResult(
    int ExpiredInvitationsCount,
    int ExpiredTrialsCount,
    int FlushedUsageBatchesCount,
    int PurgedRefreshTokensCount);

public interface IPlatformMaintenanceRunner
{
    Task<MaintenanceRunResult> RunOnceAsync(CancellationToken cancellationToken = default);
}
