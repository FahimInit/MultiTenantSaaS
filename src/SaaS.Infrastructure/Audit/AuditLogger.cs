using System.Text.Json;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.Audit;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Audit;

public sealed class AuditLogger : IAuditLogger
{
    private static readonly string[] SensitiveKeyFragments =
    [
        "password", "token", "secret", "authorization", "apikey", "api_key", "connectionstring"
    ];

    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRequestContextInfo _requestContextInfo;

    public AuditLogger(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IRequestContextInfo requestContextInfo)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _requestContextInfo = requestContextInfo;
    }

    public async Task LogTenantAsync(
        Guid tenantId,
        Guid? actorUserId,
        string actorEmail,
        AuditCategory category,
        string action,
        string targetType,
        string targetId,
        string description,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var entry = AuditLog.CreateTenantEvent(
            tenantId,
            actorUserId,
            actorEmail,
            category,
            action,
            targetType,
            targetId,
            description,
            _dateTimeProvider.UtcNow,
            SerializeSanitizedMetadata(metadata),
            _requestContextInfo.IpAddress,
            _requestContextInfo.UserAgent,
            _requestContextInfo.CorrelationId);

        await _dbContext.AuditLogs.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogPlatformAsync(
        Guid? actorUserId,
        string actorEmail,
        AuditCategory category,
        string action,
        string targetType,
        string targetId,
        string description,
        Guid? relatedTenantId = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var entry = AuditLog.CreatePlatformEvent(
            actorUserId,
            actorEmail,
            category,
            action,
            targetType,
            targetId,
            description,
            _dateTimeProvider.UtcNow,
            relatedTenantId,
            SerializeSanitizedMetadata(metadata),
            _requestContextInfo.IpAddress,
            _requestContextInfo.UserAgent,
            _requestContextInfo.CorrelationId);

        await _dbContext.AuditLogs.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? SerializeSanitizedMetadata(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in metadata)
        {
            if (IsSensitiveKey(key))
            {
                sanitized[key] = "[REDACTED]";
            }
            else
            {
                sanitized[key] = value;
            }
        }

        return JsonSerializer.Serialize(sanitized);
    }

    private static bool IsSensitiveKey(string key) =>
        SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
