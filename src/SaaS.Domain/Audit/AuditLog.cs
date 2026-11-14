using SaaS.Domain.Common;

namespace SaaS.Domain.Audit;

public enum AuditScope
{
    Tenant = 1,
    Platform = 2
}

public enum AuditCategory
{
    Identity = 1,
    Tenant = 2,
    Membership = 3,
    AccessControl = 4,
    Subscription = 5,
    Security = 6,
    Platform = 7
}

public sealed class AuditLog : Entity
{
    private AuditLog()
    {
    }

    private AuditLog(
        Guid? tenantId,
        Guid? actorUserId,
        string actorEmail,
        AuditScope scope,
        AuditCategory category,
        string action,
        string targetType,
        string targetId,
        string description,
        string? metadataJson,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        DateTimeOffset occurredAtUtc)
    {
        TenantId = tenantId;
        ActorUserId = actorUserId;
        ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "system" : actorEmail.Trim();
        Scope = scope;
        Category = category;
        Action = action.Trim();
        TargetType = targetType.Trim();
        TargetId = targetId.Trim();
        Description = description.Trim();
        MetadataJson = metadataJson;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CorrelationId = correlationId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid? TenantId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorEmail { get; private set; } = string.Empty;
    public AuditScope Scope { get; private set; }
    public AuditCategory Category { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static AuditLog CreateTenantEvent(
        Guid tenantId,
        Guid? actorUserId,
        string actorEmail,
        AuditCategory category,
        string action,
        string targetType,
        string targetId,
        string description,
        DateTimeOffset occurredAtUtc,
        string? metadataJson = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? correlationId = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("TenantId is required for a tenant-scoped audit log.");
        }

        return new AuditLog(
            tenantId,
            actorUserId,
            actorEmail,
            AuditScope.Tenant,
            category,
            action,
            targetType,
            targetId,
            description,
            metadataJson,
            ipAddress,
            userAgent,
            correlationId,
            occurredAtUtc);
    }

    public static AuditLog CreatePlatformEvent(
        Guid? actorUserId,
        string actorEmail,
        AuditCategory category,
        string action,
        string targetType,
        string targetId,
        string description,
        DateTimeOffset occurredAtUtc,
        Guid? relatedTenantId = null,
        string? metadataJson = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? correlationId = null)
    {
        return new AuditLog(
            relatedTenantId,
            actorUserId,
            actorEmail,
            AuditScope.Platform,
            category,
            action,
            targetType,
            targetId,
            description,
            metadataJson,
            ipAddress,
            userAgent,
            correlationId,
            occurredAtUtc);
    }
}
