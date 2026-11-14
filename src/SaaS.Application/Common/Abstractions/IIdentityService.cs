namespace SaaS.Application.Common.Abstractions;

public sealed record UserAccountDto(
    Guid Id,
    string Email,
    string FullName,
    bool EmailConfirmed,
    bool IsActive,
    string? PlatformRole);

public sealed record IdentityOperationResult(
    bool Succeeded,
    IReadOnlyList<string> Errors);

public enum CredentialCheckStatus
{
    Succeeded = 1,
    InvalidCredentials = 2,
    LockedOut = 3,
    InactiveUser = 4,
    EmailNotVerified = 5
}

public sealed record CredentialCheckResult(
    CredentialCheckStatus Status,
    UserAccountDto? User);

public interface IIdentityService
{
    Task<(IdentityOperationResult Result, UserAccountDto? User)> CreateUserAsync(
        string email,
        string password,
        string fullName,
        bool emailConfirmed = false,
        CancellationToken cancellationToken = default);

    Task<CredentialCheckResult> CheckPasswordSignInAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<UserAccountDto?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserAccountDto?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<string?> GenerateEmailVerificationTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken = default);
    Task<string?> GeneratePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken = default);
    Task UpdateLastLoginAsync(Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}
