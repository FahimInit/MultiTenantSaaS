using SaaS.Application.Common.Models;
using SaaS.Domain.Invitations;

namespace SaaS.Application.Common.Abstractions;

public sealed record InvitationListItemDto(
    Guid Id,
    string Email,
    Guid RoleId,
    string RoleCode,
    string RoleName,
    InvitationStatus Status,
    string InvitedByEmail,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? AcceptedAtUtc);

public sealed record InvitationAcceptanceLookup(
    TenantInvitation Invitation,
    string TenantName,
    string TenantSlug,
    string RoleCode,
    string RoleName,
    bool TenantIsOperational);

public sealed record InvitationFilterQuery(
    int Page,
    int PageSize,
    InvitationStatus? Status,
    string? Search,
    bool IsAscending);

public interface IInvitationStore
{
    Task<PagedResult<InvitationListItemDto>> ListInvitationsAsync(
        Guid tenantId,
        InvitationFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<TenantInvitation?> FindByIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken cancellationToken = default);

    Task<TenantInvitation?> FindPendingByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task<InvitationAcceptanceLookup?> FindForAcceptanceByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<int> CountPendingInvitationsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task AddInvitationAsync(TenantInvitation invitation, CancellationToken cancellationToken = default);
}
