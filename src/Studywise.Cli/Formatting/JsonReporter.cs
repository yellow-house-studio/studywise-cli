using System.Text.Json;
using System.Text.Json.Serialization;

namespace Studywise.Cli.Formatting;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> used by CLI formatters that emit JSON
/// (camelCase property names, indented output, enum names serialized as camelCase strings).
/// </summary>
public static class JsonOptions
{
    /// <summary>The shared default options instance.</summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

/// <summary>
/// Renders arbitrary values as JSON using <see cref="JsonOptions.Default"/>.
/// </summary>
public static class JsonReporter
{
    /// <summary>
    /// Serializes <paramref name="value"/> to a JSON string.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The JSON representation of <paramref name="value"/>.</returns>
    public static string Format<T>(T value) => JsonSerializer.Serialize(value, JsonOptions.Default);
}
