using System.IO;
using System.Text;

namespace FracturedJson.Formatting;

/// <summary>
/// An IBuffer for writing to a TextWriter (which will often be backed by a file or network stream).
/// Internally it composes each individual line before pushing those into writer.
/// </summary>
public class LineWriterBuffer : IBuffer
{
    public LineWriterBuffer(TextWriter writer, bool trimTrailingWhitespace)
    {
        _writer = writer;
        _trimTrailingWhitespace = trimTrailingWhitespace;
    }

    public IBuffer Add(string value)
    {
        _lineBuff.Append(value);
        return this;
    }

    public IBuffer Add(params string[] values)
    {
        foreach(var item in values)
            _lineBuff.Append(item);
        return this;
    }

    public IBuffer EndLine(string eolString)
    {
        AddLineToWriter(eolString);
        return this;
    }

    public IBuffer Flush()
    {
        AddLineToWriter(string.Empty);
        _writer.Flush();
        return this;
    }

    private readonly TextWriter _writer;
    private readonly bool _trimTrailingWhitespace;
    private readonly StringBuilder _lineBuff = new();

    private void AddLineToWriter(string eolString)
    {
        if (_lineBuff.Length == 0 && eolString.Length == 0)
            return;

        var newLength = _lineBuff.Length;
        if (_trimTrailingWhitespace)
        {
            // Figure out where the end of the line's non-whitespace characters is.
            while (newLength > 0)
            {
                var ch = _lineBuff[newLength - 1];
                if (ch is not (' ' or '\t'))
                    break;
                newLength -= 1;
            }
        }

        // Write only up to the selected end to the Writer.
        _writer.Write(_lineBuff.ToString(0, newLength));
        _writer.Write(eolString);
        _lineBuff.Clear();
    }
}
