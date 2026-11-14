namespace SaaS.Application.Common.Abstractions;

/// <summary>
/// Provides deterministic UTC timestamps for domain and application workflows.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
