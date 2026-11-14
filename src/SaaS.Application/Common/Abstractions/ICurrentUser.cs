namespace SaaS.Application.Common.Abstractions;

/// <summary>
/// Read-only abstraction over the authenticated caller's identity claims.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsPlatformAdmin { get; }
}
