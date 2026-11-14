namespace SaaS.Application.Common.Abstractions;

public interface IOpaqueTokenService
{
    (string RawToken, string TokenHash) GenerateTokenPair();
    string HashToken(string rawToken);
}
