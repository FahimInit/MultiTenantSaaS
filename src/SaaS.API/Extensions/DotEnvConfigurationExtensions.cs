using Microsoft.Extensions.Configuration.Memory;

namespace SaaS.API.Extensions;

public static class DotEnvConfigurationExtensions
{
    /// <summary>
    /// Loads key-value pairs from a local untracked .env file in Development at lowest precedence
    /// so `dotnet run` works out-of-the-box without committing secrets to appsettings.json,
    /// while allowing Docker environment variables and integration test settings to override.
    /// </summary>
    public static WebApplicationBuilder LoadDotEnvIfDevelopment(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            return builder;
        }

        var envFilePath = FindDotEnvFile(builder.Environment.ContentRootPath);
        if (envFilePath is null)
        {
            return builder;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(envFilePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var rawKey = line[..separatorIndex].Trim();
            var rawValue = line[(separatorIndex + 1)..].Trim();

            if (rawValue.Length >= 2 &&
                ((rawValue.StartsWith('"') && rawValue.EndsWith('"')) ||
                 (rawValue.StartsWith('\'') && rawValue.EndsWith('\''))))
            {
                rawValue = rawValue[1..^1];
            }

            var normalizedKey = rawKey.Replace("__", ":", StringComparison.Ordinal);
            values[normalizedKey] = rawValue;
        }

        if (values.Count > 0)
        {
            builder.Configuration.Sources.Insert(0, new MemoryConfigurationSource
            {
                InitialData = values
            });
        }

        return builder;
    }

    private static string? FindDotEnvFile(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        for (var depth = 0; current is not null && depth < 5; depth++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
