using System.IO;

namespace FracturedJson.Formatting;

public class TextWriterBuffer : IBuffer
{
    public TextWriterBuffer(TextWriter writer)
    {
        _writer = writer;
    }

    public void Add(string value)
    {
        _writer.Write(value);
    }

    public void Add(params string[] values)
    {
        foreach (var val in values)
            _writer.Write(val);
    }

    private readonly TextWriter _writer;
}