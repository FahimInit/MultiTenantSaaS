using SaaS.Application.Common.Abstractions;

namespace SaaS.Application.Modules.Identity;

public sealed record AuthenticatedUserDto(
    Guid Id,
    string Email,
    string FullName,
    string? PlatformRole);

public sealed record AuthTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    AuthenticatedUserDto User,
    IReadOnlyList<UserTenantSummaryDto> Tenants);

public sealed record RegisterUserResponse(
    Guid UserId,
    string Email,
    bool EmailVerificationRequired);

public sealed record UserSessionDto(
    Guid Id,
    string IpAddress,
    string UserAgent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    bool IsActive);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FullName,
    bool EmailConfirmed,
    string? PlatformRole,
    IReadOnlyList<UserTenantSummaryDto> Tenants);
