namespace SaaS.Domain.Common;

/// <summary>
/// Represents an invariant or business rule violation inside the domain model.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message, string code = "domain_rule_violation")
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
