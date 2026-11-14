using SaaS.Domain.Common;

namespace SaaS.Domain.Identity;

public sealed class UserSession : AggregateRoot
{
    private UserSession()
    {
    }

    public UserSession(Guid userId, string ipAddress, string userAgent, DateTimeOffset createdAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId is required for a user session.");
        }

        UserId = userId;
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress.Trim();
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? "unknown" : Truncate(userAgent.Trim(), 512);
        CreatedAtUtc = createdAtUtc;
        LastSeenAtUtc = createdAtUtc;
    }

    public Guid UserId { get; private set; }
    public string IpAddress { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsActive => RevokedAtUtc is null;

    public void Touch(DateTimeOffset seenAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        LastSeenAtUtc = seenAtUtc;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        if (RevokedAtUtc is null)
        {
            RevokedAtUtc = revokedAtUtc;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
