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

    private const int _copyArraySize = 2048;
    private readonly char[] _lineCopyArray = new char[_copyArraySize];

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

        if (_lineBuff.Length > _lineCopyArray.Length)
        {
            // If the line is really long, go ahead an allocate a new string temporarily.  It spikes memory a little,
            // briefly, but it's faster than chunking the line data.  This should be a pretty uncommon case.
            _writer.Write(_lineBuff.ToString());
        }
        else
        {
            // Normal case - use a reusable char[] to transfer the line from _lineBuff to _writer.  No need to allocate
            // a new string for each one.
            _lineBuff.CopyTo(0, _lineCopyArray, 0, _lineBuff.Length);
            _writer.Write(_lineCopyArray, 0, _lineBuff.Length);
        }

        _lineBuff.Clear();
    }
}
