using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using SaaS.Application.Common.Abstractions;

namespace SaaS.Infrastructure.Identity;

public sealed class OpaqueTokenService : IOpaqueTokenService
{
    public (string RawToken, string TokenHash) GenerateTokenPair()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = WebEncoders.Base64UrlEncode(bytes);
        var hash = HashToken(rawToken);
        return (rawToken, hash);
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
