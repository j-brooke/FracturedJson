using System.Text;

namespace FracturedJson.Formatting;

/// <summary>
/// A place where strings are piled up sequentially to eventually make one big string, implemented with a
/// good old .NET StringBuilder.
/// </summary>
public class StringBuilderBuffer : IBuffer
{
    public StringBuilderBuffer(bool trimTrailingWhitespace)
    {
        _trimTrailingWhitespace = trimTrailingWhitespace;
    }

    public IBuffer Add(string value)
    {
        _buff.Append(value);
        return this;
    }

    public IBuffer Add(params string[] values)
    {
        foreach (var val in values)
            _buff.Append(val);
        return this;
    }

    public IBuffer EndLine(string eolString)
    {
        TrimIfNeeded();
        _buff.Append(eolString);
        return this;
    }

    public IBuffer Flush()
    {
        TrimIfNeeded();
        return this;
    }

    public string AsString()
    {
        return _buff.ToString();
    }

    private readonly StringBuilder _buff = new();
    private readonly bool _trimTrailingWhitespace;

    /// <summary>
    /// Gets rid of spaces and tabs at the end of the buffer.  This should be called at the end of a line
    /// but before the EOL symbol is added.
    /// </summary>
    private void TrimIfNeeded()
    {
        if (!_trimTrailingWhitespace)
            return;

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
