using SaaS.Domain.Identity;
using SaaS.Domain.Tenants;

namespace SaaS.Application.Common.Abstractions;

public sealed record ResolvedTenantMembership(
    Guid TenantId,
    Guid MembershipId,
    TenantStatus TenantStatus,
    string RoleCode,
    IReadOnlyList<string> Permissions);

public interface ITenantMembershipResolver
{
    Task<ResolvedTenantMembership?> ResolveAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}

public interface ISessionStore
{
    Task AddSessionAsync(UserSession session, CancellationToken cancellationToken = default);
    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<UserSession?> FindSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSession>> ListActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeTokenFamilyAndSessionAsync(Guid tokenFamilyId, Guid sessionId, string reason, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
    Task RevokeSessionAndTokensAsync(Guid sessionId, string reason, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
    Task RevokeAllUserSessionsAsync(Guid userId, string reason, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}
