using System.Text.Json;
using System.Text.Json.Serialization;

namespace Studywise.Cli.Formatting;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

public static class JsonReporter
{
    public static string Format<T>(T value) => JsonSerializer.Serialize(value, JsonOptions.Default);
}