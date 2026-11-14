using SaaS.Domain.Common;

namespace SaaS.Domain.Identity;

public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        Guid sessionId,
        Guid tokenFamilyId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId is required for refresh token.");
        }

        if (sessionId == Guid.Empty)
        {
            throw new DomainException("SessionId is required for refresh token.");
        }

        if (tokenFamilyId == Guid.Empty)
        {
            throw new DomainException("TokenFamilyId is required for refresh token.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("TokenHash cannot be empty.");
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new DomainException("Refresh token expiry must be in the future.");
        }

        UserId = userId;
        SessionId = sessionId;
        TokenFamilyId = tokenFamilyId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid UserId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid TokenFamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired(DateTimeOffset utcNow) => utcNow >= ExpiresAtUtc;

    public bool IsActive(DateTimeOffset utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(string reason, DateTimeOffset revokedAtUtc, Guid? replacedByTokenId = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAtUtc = revokedAtUtc;
        RevokedReason = string.IsNullOrWhiteSpace(reason) ? "Revoked" : reason.Trim();
        ReplacedByTokenId = replacedByTokenId;
    }
}
