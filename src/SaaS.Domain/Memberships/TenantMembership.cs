using SaaS.Domain.Common;

namespace SaaS.Domain.Memberships;

public sealed class TenantMembership : TenantEntity
{
    private TenantMembership()
    {
    }

    public TenantMembership(
        Guid tenantId,
        Guid userId,
        Guid roleId,
        DateTimeOffset joinedAtUtc,
        Guid? invitedByUserId = null)
        : base(tenantId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId is required for tenant membership.");
        }

        if (roleId == Guid.Empty)
        {
            throw new DomainException("RoleId is required for tenant membership.");
        }

        UserId = userId;
        RoleId = roleId;
        Status = MembershipStatus.Active;
        InvitedByUserId = invitedByUserId;
        JoinedAtUtc = joinedAtUtc;
        UpdatedAtUtc = joinedAtUtc;
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public MembershipStatus Status { get; private set; }
    public Guid? InvitedByUserId { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsActive => Status == MembershipStatus.Active;

    public void ChangeRole(Guid newRoleId, DateTimeOffset utcNow)
    {
        if (newRoleId == Guid.Empty)
        {
            throw new DomainException("RoleId cannot be empty.");
        }

        if (Status == MembershipStatus.Removed)
        {
            throw new DomainException("Cannot change the role of a removed member.", "invalid_membership_state");
        }

        RoleId = newRoleId;
        UpdatedAtUtc = utcNow;
    }

    public void Suspend(DateTimeOffset utcNow)
    {
        if (Status == MembershipStatus.Removed)
        {
            throw new DomainException("Cannot suspend a removed member.", "invalid_membership_state");
        }

        Status = MembershipStatus.Suspended;
        UpdatedAtUtc = utcNow;
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (Status == MembershipStatus.Removed)
        {
            throw new DomainException("Cannot reactivate a removed member.", "invalid_membership_state");
        }

        Status = MembershipStatus.Active;
        UpdatedAtUtc = utcNow;
    }

    public void Remove(DateTimeOffset utcNow)
    {
        Status = MembershipStatus.Removed;
        UpdatedAtUtc = utcNow;
    }

    public void Rejoin(Guid roleId, Guid? invitedByUserId, DateTimeOffset utcNow)
    {
        if (roleId == Guid.Empty)
        {
            throw new DomainException("RoleId cannot be empty.");
        }

        if (Status != MembershipStatus.Removed)
        {
            throw new DomainException("Only a removed membership can be rejoined.", "invalid_membership_state");
        }

        RoleId = roleId;
        InvitedByUserId = invitedByUserId;
        Status = MembershipStatus.Active;
        JoinedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }
}
