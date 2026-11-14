namespace SaaS.Application.Common.Abstractions;

public sealed record GeneratedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);

public sealed record GeneratedRefreshToken(string RawToken, string TokenHash, DateTimeOffset ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    GeneratedAccessToken GenerateAccessToken(UserAccountDto user, Guid sessionId);
    GeneratedRefreshToken GenerateRefreshToken();
    string HashRefreshToken(string rawToken);
}
