using System.Text.RegularExpressions;

namespace BWare.Services;

/// <summary>
/// Decomposed Firebase URL for internal use.
/// </summary>
public record ParsedFirebaseUrl
{
    /// <summary>
    /// Root URL without path (e.g., "https://project.region.firebasedatabase.app").
    /// </summary>
    public required string RootUrl { get; init; }

    /// <summary>
    /// Firebase project identifier.
    /// </summary>
    public required string ProjectId { get; init; }

    /// <summary>
    /// Database region (e.g., "asia-southeast1"). Null for legacy US databases.
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// Database path (e.g., "alerts"). Defaults to empty if not specified.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the full URL for SSE connection (with .json suffix).
    /// </summary>
    public string SseUrl => string.IsNullOrEmpty(Path)
        ? $"{RootUrl}/.json"
        : $"{RootUrl}/{Path}.json";

    /// <summary>
    /// Gets the full URL for REST API operations (with .json suffix).
    /// </summary>
    public string RestUrl => SseUrl;
}

/// <summary>
/// Firebase URL parsing and validation utilities.
/// </summary>
public static class FirebaseConfig
{
    // Pattern for regional Firebase RTDB: https://PROJECT.REGION.firebasedatabase.app/path
    private static readonly Regex RegionalPattern = new(
        @"^https://([a-zA-Z0-9-]+)\.([a-zA-Z0-9-]+)\.firebasedatabase\.app(?:/(.*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Pattern for legacy Firebase RTDB: https://PROJECT.firebaseio.com/path
    private static readonly Regex LegacyPattern = new(
        @"^https://([a-zA-Z0-9-]+)\.firebaseio\.com(?:/(.*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates if a URL is a valid Firebase Realtime Database URL.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return RegionalPattern.IsMatch(url) || LegacyPattern.IsMatch(url);
    }

    /// <summary>
    /// Parses a Firebase URL into its components.
    /// </summary>
    /// <param name="url">The Firebase URL to parse.</param>
    /// <returns>Parsed URL components, or null if invalid.</returns>
    public static ParsedFirebaseUrl? Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Try regional format first
        var regionalMatch = RegionalPattern.Match(url);
        if (regionalMatch.Success)
        {
            var projectId = regionalMatch.Groups[1].Value;
            var region = regionalMatch.Groups[2].Value;
            var path = regionalMatch.Groups[3].Success ? regionalMatch.Groups[3].Value.TrimEnd('/') : "";

            // Remove .json suffix if present (will be added automatically by SseUrl/RestUrl)
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(0, path.Length - 5);
            }

            return new ParsedFirebaseUrl
            {
                RootUrl = $"https://{projectId}.{region}.firebasedatabase.app",
                ProjectId = projectId,
                Region = region,
                Path = path
            };
        }

        // Try legacy format
        var legacyMatch = LegacyPattern.Match(url);
        if (legacyMatch.Success)
        {
            var projectId = legacyMatch.Groups[1].Value;
            var path = legacyMatch.Groups[2].Success ? legacyMatch.Groups[2].Value.TrimEnd('/') : "";

            // Remove .json suffix if present (will be added automatically by SseUrl/RestUrl)
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(0, path.Length - 5);
            }

            return new ParsedFirebaseUrl
            {
                RootUrl = $"https://{projectId}.firebaseio.com",
                ProjectId = projectId,
                Region = null,
                Path = path
            };
        }

        return null;
    }

    /// <summary>
    /// Validates a URL and returns an error message if invalid.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>Error message if invalid, null if valid.</returns>
    public static string? GetValidationError(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "Firebase URL is required.";

        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "Firebase URL must start with https://";

        if (!IsValidUrl(url))
            return "Invalid Firebase URL format. Expected: https://PROJECT.firebaseio.com/path or https://PROJECT.REGION.firebasedatabase.app/path";

        return null;
    }
}
