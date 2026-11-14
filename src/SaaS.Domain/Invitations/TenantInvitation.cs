using SaaS.Domain.Common;

namespace SaaS.Domain.Invitations;

public sealed class TenantInvitation : TenantEntity
{
    private TenantInvitation()
    {
    }

    public TenantInvitation(
        Guid tenantId,
        string email,
        Guid roleId,
        string tokenHash,
        Guid invitedByUserId,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset createdAtUtc)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new DomainException("A valid recipient email address is required.", "invalid_invitation_email");
        }

        if (roleId == Guid.Empty)
        {
            throw new DomainException("A target role is required for an invitation.", "invalid_role_id");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Invitation token hash is required.");
        }

        if (invitedByUserId == Guid.Empty)
        {
            throw new DomainException("InvitedByUserId is required.");
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new DomainException("Invitation expiration timestamp must be in the future.", "invalid_invitation_expiry");
        }

        Email = email.Trim();
        NormalizedEmail = Email.ToLowerInvariant();
        RoleId = roleId;
        TokenHash = tokenHash.Trim();
        Status = InvitationStatus.Pending;
        InvitedByUserId = invitedByUserId;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public Guid RoleId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public InvitationStatus Status { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool IsExpiredAt(DateTimeOffset utcNow) =>
        Status == InvitationStatus.Expired || (Status == InvitationStatus.Pending && ExpiresAtUtc <= utcNow);

    public void RefreshForResend(string newTokenHash, DateTimeOffset newExpiresAtUtc, DateTimeOffset utcNow)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new DomainException("Only pending invitations can be resent.", "invalid_invitation_status");
        }

        if (string.IsNullOrWhiteSpace(newTokenHash))
        {
            throw new DomainException("New invitation token hash is required.");
        }

        if (newExpiresAtUtc <= utcNow)
        {
            throw new DomainException("Invitation expiration timestamp must be in the future.", "invalid_invitation_expiry");
        }

        TokenHash = newTokenHash.Trim();
        ExpiresAtUtc = newExpiresAtUtc;
    }

    public void Accept(Guid acceptingUserId, string acceptingUserEmail, DateTimeOffset utcNow)
    {
        if (acceptingUserId == Guid.Empty)
        {
            throw new DomainException("Accepting user identifier is required.");
        }

        if (Status != InvitationStatus.Pending)
        {
            throw new DomainException($"Invitation is no longer active (current status: {Status}).", "invitation_not_pending");
        }

        if (ExpiresAtUtc <= utcNow)
        {
            Status = InvitationStatus.Expired;
            throw new DomainException("This invitation has expired.", "invitation_expired");
        }

        var normalizedCallerEmail = acceptingUserEmail?.Trim().ToLowerInvariant();
        if (!string.Equals(NormalizedEmail, normalizedCallerEmail, StringComparison.Ordinal))
        {
            throw new DomainException(
                "This invitation was issued for a different email address.",
                "invitation_email_mismatch");
        }

        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = utcNow;
        AcceptedByUserId = acceptingUserId;
    }

    public void Revoke(DateTimeOffset utcNow)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new DomainException("Only pending invitations can be revoked.", "invalid_invitation_status");
        }

        Status = InvitationStatus.Revoked;
        RevokedAtUtc = utcNow;
    }

    public void MarkExpired()
    {
        if (Status == InvitationStatus.Pending)
        {
            Status = InvitationStatus.Expired;
        }
    }
}
