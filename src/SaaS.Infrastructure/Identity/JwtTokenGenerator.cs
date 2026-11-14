using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SaaS.Application.Common.Abstractions;

namespace SaaS.Infrastructure.Identity;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;

    public JwtTokenGenerator(IOptions<JwtSettings> options, IDateTimeProvider dateTimeProvider)
    {
        _settings = options.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public GeneratedAccessToken GenerateAccessToken(UserAccountDto user, Guid sessionId)
    {
        if (string.IsNullOrWhiteSpace(_settings.SigningKey) || _settings.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        }

        var now = _dateTimeProvider.UtcNow;
        var expiresAt = now.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("session_id", sessionId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.PlatformRole))
        {
            claims.Add(new Claim("platform_role", user.PlatformRole));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new GeneratedAccessToken(encoded, expiresAt);
    }

    public GeneratedRefreshToken GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var hash = HashRefreshToken(rawToken);
        var expiresAt = _dateTimeProvider.UtcNow.AddDays(_settings.RefreshTokenDays);

        return new GeneratedRefreshToken(rawToken, hash, expiresAt);
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
