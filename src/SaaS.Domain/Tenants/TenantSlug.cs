using System.Text.RegularExpressions;
using SaaS.Domain.Common;

namespace SaaS.Domain.Tenants;

public sealed partial class TenantSlug : ValueObject
{
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "app", "auth", "billing", "dashboard", "health",
        "login", "logout", "platform", "register", "root", "settings",
        "swagger", "system", "tenant", "tenants", "www"
    };

    private TenantSlug()
    {
    }

    private TenantSlug(string value)
    {
        Value = value;
    }

    public string Value { get; } = string.Empty;

    public static TenantSlug Create(string rawSlug)
    {
        if (string.IsNullOrWhiteSpace(rawSlug))
        {
            throw new DomainException("Organization slug is required.", "invalid_tenant_slug");
        }

        var normalized = rawSlug.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 63 || !SlugRegex().IsMatch(normalized))
        {
            throw new DomainException(
                "Slug must be 3-63 characters using lowercase letters, numbers, and single hyphens (cannot start or end with a hyphen).",
                "invalid_tenant_slug");
        }

        if (ReservedSlugs.Contains(normalized))
        {
            throw new DomainException($"The slug '{normalized}' is reserved and cannot be used.", "reserved_tenant_slug");
        }

        return new TenantSlug(normalized);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex SlugRegex();
}
