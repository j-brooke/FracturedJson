using System.IO;
using System.Text;

namespace FracturedJson.Formatting;

/// <summary>
/// An IBuffer for writing to a TextWriter (which will often be backed by a file or network stream).
/// Internally it composes each individual line before pushing those into writer.
/// </summary>
public class LineWriterBuffer : IBuffer
{
    /// <summary>
    /// Creates a new LineWriterBuffer.
    /// </summary>
    /// <param name="writer">TextWriter to which the sequence should be written.</param>
    public LineWriterBuffer(TextWriter writer)
    {
        _writer = writer;
    }

    /// <summary>
    /// Add a single string to the buffer.
    /// </summary>
    public IBuffer Add(string value)
    {
        _lineBuff.Append(value);
        return this;
    }

    /// <summary>
    /// Add a group of strings to the buffer.
    /// </summary>
    public IBuffer Add(params string[] values)
    {
        foreach(var item in values)
            _lineBuff.Append(item);
        return this;
    }

    /// <summary>
    /// Adds the requested number of spaces to the buffer.
    /// </summary>
    public IBuffer Spaces(int count)
    {
        _lineBuff.Append(' ', count);
        return this;
    }

    /// <summary>
    /// Call this only when sending an end-of-line symbol to the buffer.  Doing so helps the buffer with
    /// extra post-processing, like trimming trailing whitespace.
    /// </summary>
    public IBuffer EndLine(string eolString)
    {
        AddLineToWriter(eolString);
        return this;
    }

    /// <summary>
    /// Call this to let the buffer finish up any work in progress.
    /// </summary>
    public IBuffer Flush()
    {
        AddLineToWriter(string.Empty);
        _writer.Flush();
        return this;
    }

    private readonly TextWriter _writer;
    private readonly StringBuilder _lineBuff = new();

    private void AddLineToWriter(string eolString)
    {
        if (_lineBuff.Length == 0 && eolString.Length == 0)
            return;

        // Figure out where the end of the line's non-whitespace characters is.
        var newLength = _lineBuff.Length;
        while (newLength > 0)
        {
            var ch = _lineBuff[newLength - 1];
            if (ch is not (' ' or '\t'))
                break;
            newLength -= 1;
        }
        _lineBuff.Length = newLength;
        _lineBuff.Append(eolString);

        // Write only up to the selected end to the Writer.
        _writer.Write(_lineBuff.ToString());
        _lineBuff.Clear();
    }
}
