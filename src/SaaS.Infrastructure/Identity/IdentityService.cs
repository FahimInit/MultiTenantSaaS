using Microsoft.AspNetCore.Identity;
using SaaS.Application.Common.Abstractions;

namespace SaaS.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDateTimeProvider _dateTimeProvider;

    public IdentityService(UserManager<ApplicationUser> userManager, IDateTimeProvider dateTimeProvider)
    {
        _userManager = userManager;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<(IdentityOperationResult Result, UserAccountDto? User)> CreateUserAsync(
        string email,
        string password,
        string fullName,
        bool emailConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = normalized,
            Email = normalized,
            FullName = fullName.Trim(),
            EmailConfirmed = emailConfirmed,
            IsActive = true,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        var identityResult = await _userManager.CreateAsync(user, password);
        if (!identityResult.Succeeded)
        {
            return (
                new IdentityOperationResult(false, identityResult.Errors.Select(e => e.Description).ToList()),
                null);
        }

        return (new IdentityOperationResult(true, []), ToDto(user));
    }

    public async Task<CredentialCheckResult> CheckPasswordSignInAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return new CredentialCheckResult(CredentialCheckStatus.InvalidCredentials, null);
        }

        if (!user.IsActive)
        {
            return new CredentialCheckResult(CredentialCheckStatus.InactiveUser, ToDto(user));
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return new CredentialCheckResult(CredentialCheckStatus.LockedOut, ToDto(user));
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
            {
                return new CredentialCheckResult(CredentialCheckStatus.LockedOut, ToDto(user));
            }

            return new CredentialCheckResult(CredentialCheckStatus.InvalidCredentials, null);
        }

        if (!user.EmailConfirmed)
        {
            return new CredentialCheckResult(CredentialCheckStatus.EmailNotVerified, ToDto(user));
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        return new CredentialCheckResult(CredentialCheckStatus.Succeeded, ToDto(user));
    }

    public async Task<UserAccountDto?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : ToDto(user);
    }

    public async Task<UserAccountDto?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        return user is null ? null : ToDto(user);
    }

    public async Task<string?> GenerateEmailVerificationTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        return await _userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task<IdentityOperationResult> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return new IdentityOperationResult(false, ["User not found."]);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return new IdentityOperationResult(result.Succeeded, result.Errors.Select(e => e.Description).ToList());
    }

    public async Task<string?> GeneratePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<IdentityOperationResult> ResetPasswordAsync(
        Guid userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return new IdentityOperationResult(false, ["Invalid password reset request."]);
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }

        return new IdentityOperationResult(result.Succeeded, result.Errors.Select(e => e.Description).ToList());
    }

    public async Task UpdateLastLoginAsync(Guid userId, DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return;
        }

        user.LastLoginAtUtc = utcNow;
        await _userManager.UpdateAsync(user);
    }

    private static UserAccountDto ToDto(ApplicationUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.FullName, user.EmailConfirmed, user.IsActive, user.PlatformRole);
}
