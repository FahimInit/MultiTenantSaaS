using Microsoft.AspNetCore.Identity;

namespace SaaS.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity user entity kept in Infrastructure so SaaS.Domain stays framework-agnostic.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string? PlatformRole { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
