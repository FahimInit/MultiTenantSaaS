namespace SaaS.Application.Common.Abstractions;

public interface ISubscriptionQuotaService
{
    Task EnsureWorkspaceWriteAllowedAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureCanAddMemberOrInviteAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureCanCreateCustomRoleAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task SyncUsageCountsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task SyncUsageCountsUnfilteredAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface ISubscriptionProvisioner
{
    Task ProvisionInitialSubscriptionAsync(
        Guid tenantId,
        Guid actorUserId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}
