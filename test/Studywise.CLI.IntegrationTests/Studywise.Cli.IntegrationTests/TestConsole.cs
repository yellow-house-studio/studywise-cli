using System.CommandLine;
using System.CommandLine.IO;

namespace Studywise.CLI.IntegrationTests;

/// <summary>
/// In-memory <see cref="IConsole"/> for integration tests. Mirrors the unit test
/// project's helper — the two test projects do not share a project reference.
/// </summary>
internal sealed class TestConsole : IConsole
{
    /// <summary>Lines written to stdout.</summary>
    public List<string> Lines { get; } = [];

    /// <summary>Writer behind the error stream.</summary>
    public StringWriter ErrorWriter { get; } = new StringWriter();

    /// <summary>Everything written to the error stream.</summary>
    public string ErrorOutput => ErrorWriter.ToString();

    /// <inheritdoc />
    public IStandardStreamWriter Out => new CapturingWriter(Lines);

    /// <inheritdoc />
    public IStandardStreamWriter Error => new CapturingWriter(null, ErrorWriter);

    /// <inheritdoc />
    public bool IsOutputRedirected => false;

    /// <inheritdoc />
    public bool IsErrorRedirected => false;

    /// <inheritdoc />
    public bool IsInputRedirected => false;

    private sealed class CapturingWriter(List<string>? lines, StringWriter? writer = null) : IStandardStreamWriter
    {
        public void Write(string? value)
        {
            if (lines is not null)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    lines.Add(value);
                }
            }
            else
            {
                writer!.Write(value ?? string.Empty);
            }
        }

        public void WriteLine(string? value)
        {
            if (lines is not null)
            {
                lines.Add(value ?? string.Empty);
            }
            else
            {
                writer!.WriteLine(value ?? string.Empty);
            }
        }

        public void Write(char[] buffer, int index, int count)
        {
            var text = new string(buffer, index, count);
            if (lines is not null)
            {
                if (count > 0)
                {
                    lines.Add(text);
                }
            }
            else
            {
                writer!.Write(buffer, index, count);
            }
        }
    }
}
