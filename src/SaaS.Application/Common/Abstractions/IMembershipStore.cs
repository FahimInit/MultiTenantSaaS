using SaaS.Application.Common.Models;
using SaaS.Domain.Memberships;

namespace SaaS.Application.Common.Abstractions;

public sealed record MemberListItemDto(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string FullName,
    Guid RoleId,
    string RoleCode,
    string RoleName,
    bool IsSystemRole,
    MembershipStatus Status,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset? LastLoginAtUtc);

public sealed record MemberDetailWithRole(
    TenantMembership Membership,
    string UserEmail,
    string UserFullName,
    string RoleCode,
    string RoleName);

public sealed record MemberFilterQuery(
    int Page,
    int PageSize,
    string? Search,
    MembershipStatus? Status,
    Guid? RoleId,
    string? SortBy,
    bool IsAscending);

public interface IMembershipStore
{
    Task<PagedResult<MemberListItemDto>> ListMembersAsync(
        Guid tenantId,
        MemberFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<MemberDetailWithRole?> FindMemberDetailAsync(
        Guid tenantId,
        Guid membershipId,
        CancellationToken cancellationToken = default);

    Task<TenantMembership?> FindByTenantAndUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Intentionally bypasses ITenantContext query filter for user-scoped invitation acceptance
    /// and cross-tenant provisioning flows where tenantId has already been verified cryptographically.
    /// </summary>
    Task<TenantMembership?> FindByTenantAndUserUnfilteredAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveMembersUnfilteredAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveOrSuspendedMemberByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveMembersAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<int> CountActiveOwnersAsync(
        Guid tenantId,
        Guid? excludeMembershipId = null,
        CancellationToken cancellationToken = default);
}
