using System.Text;

namespace FracturedJson.Formatting;

/// <summary>
/// A place where strings are piled up sequentially to eventually make one big string, implemented with a
/// good old .NET StringBuilder.
/// </summary>
public class StringBuilderBuffer : IBuffer, ILinePeeker
{
    /// <summary>
    /// Add a single string to the buffer.
    /// </summary>
    public IBuffer Add(string value)
    {
        _buff.Append(value);
        return this;
    }

    /// <summary>
    /// Add a group of strings to the buffer.
    /// </summary>
    public IBuffer Add(params string[] values)
    {
        foreach (var val in values)
            _buff.Append(val);
        return this;
    }

    /// <summary>
    /// Adds the requested number of spaces to the buffer.
    /// </summary>
    public IBuffer Spaces(int count)
    {
        _buff.Append(' ', count);
        return this;
    }

    /// <summary>
    /// Call this only when sending an end-of-line symbol to the buffer.  Doing so helps the buffer with
    /// extra post-processing, like trimming trailing whitespace.
    /// </summary>
    public IBuffer EndLine(string eolString)
    {
        TrimIfNeeded();
        _buff.Append(eolString);
        _startOfLineIndex = _buff.Length;
        return this;
    }

    /// <summary>
    /// Call this to let the buffer finish up any work in progress.
    /// </summary>
    public IBuffer Flush()
    {
        TrimIfNeeded();
        return this;
    }

    public string PeekCurrentLine()
    {
        return _buff.ToString(_startOfLineIndex, _buff.Length - _startOfLineIndex);
    }

    /// <summary>
    /// Convert the contents of the buffer into a single string.
    /// </summary>
    public string AsString()
    {
        return _buff.ToString();
    }

    private readonly StringBuilder _buff = new();
    private int _startOfLineIndex = 0;

    /// <summary>
    /// Gets rid of spaces and tabs at the end of the buffer.  This should be called at the end of a line
    /// but before the EOL symbol is added.
    /// </summary>
    private void TrimIfNeeded()
    {
        var newLength = _buff.Length;
        while (newLength > 0)
        {
            var ch = _buff[newLength - 1];
            if (ch is not (' ' or '\t'))
                break;
            newLength -= 1;
        }

        _buff.Length = newLength;
    }
}
