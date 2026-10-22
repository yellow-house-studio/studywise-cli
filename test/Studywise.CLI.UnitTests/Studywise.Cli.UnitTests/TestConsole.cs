using System.CommandLine;
using System.CommandLine.IO;

namespace Studywise.CLI.UnitTests;

/// <summary>
/// In-memory <see cref="IConsole"/> for command-handler tests. Captures the stdout
/// lines and the error stream separately, so a test can assert what landed on which
/// channel.
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
    public IStandardStreamWriter Out => new TestStreamWriter(Lines);

    /// <inheritdoc />
    public IStandardStreamWriter Error => new StringWriterStreamWriter(ErrorWriter);

    /// <inheritdoc />
    public bool IsOutputRedirected => false;

    /// <inheritdoc />
    public bool IsErrorRedirected => false;

    /// <inheritdoc />
    public bool IsInputRedirected => false;
}

/// <summary>
/// <see cref="IStandardStreamWriter"/> that appends to a <see cref="StringWriter"/>.
/// </summary>
internal sealed class StringWriterStreamWriter(StringWriter writer) : IStandardStreamWriter
{
    /// <inheritdoc />
    public void Write(string? value) => writer.Write(value ?? string.Empty);

    /// <inheritdoc />
    public void WriteLine(string? value) => writer.WriteLine(value ?? string.Empty);

    /// <inheritdoc />
    public void Write(char[] buffer, int index, int count) => writer.Write(buffer, index, count);
}

/// <summary>
/// <see cref="IStandardStreamWriter"/> that appends whole lines to a list.
/// </summary>
internal sealed class TestStreamWriter(List<string> lines) : IStandardStreamWriter
{
    /// <inheritdoc />
    public void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            lines.Add(value);
        }
    }

    /// <inheritdoc />
    public void WriteLine(string? value) => lines.Add(value ?? string.Empty);

    /// <inheritdoc />
    public void Write(char[] buffer, int index, int count)
    {
        if (count > 0)
        {
            lines.Add(new string(buffer, index, count));
        }
    }
}
