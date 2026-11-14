using SaaS.Application.Common.Models;
using SaaS.Domain.Audit;

namespace SaaS.Application.Common.Abstractions;

public sealed record AuditLogDto(
    Guid Id,
    Guid? TenantId,
    string? TenantName,
    Guid? ActorUserId,
    string ActorEmail,
    AuditScope Scope,
    AuditCategory Category,
    string Action,
    string TargetType,
    string TargetId,
    string Description,
    string? MetadataJson,
    string? IpAddress,
    string? CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record TenantAuditFilterQuery(
    int Page,
    int PageSize,
    AuditCategory? Category,
    string? Action,
    Guid? ActorUserId,
    string? Search,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    bool IsAscending);

public sealed record PlatformAuditFilterQuery(
    int Page,
    int PageSize,
    Guid? TenantId,
    AuditScope? Scope,
    AuditCategory? Category,
    string? Action,
    string? Search,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    bool IsAscending);

public interface IAuditQueryStore
{
    Task<PagedResult<AuditLogDto>> QueryTenantLogsAsync(
        Guid tenantId,
        TenantAuditFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AuditLogDto>> QueryPlatformLogsAsync(
        PlatformAuditFilterQuery query,
        CancellationToken cancellationToken = default);
}
