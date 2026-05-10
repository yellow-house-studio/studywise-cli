using System.Text.Json;
using System.Text.Json.Serialization;

namespace Studywise.Cli.Diagnostics.Formatting;

public sealed class JsonDiagnosticReportFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Format(DiagnosticReport report)
    {
        return JsonSerializer.Serialize(report, SerializerOptions);
    }
}
