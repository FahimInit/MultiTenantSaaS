using SaaS.Domain.Audit;

namespace SaaS.Application.Common.Abstractions;

public interface IRequestContextInfo
{
    string IpAddress { get; }
    string UserAgent { get; }
    string CorrelationId { get; }
}

public interface IAuditLogger
{
    Task LogTenantAsync(
        Guid tenantId,
        Guid? actorUserId,
        string actorEmail,
        AuditCategory category,
        string action,
        string targetType,
        string targetId,
        string description,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);

    Task LogPlatformAsync(
        Guid? actorUserId,
        string actorEmail,
        AuditCategory category,
        string action,
        string targetType,
        string targetId,
        string description,
        Guid? relatedTenantId = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);
}
