using System.Text;

namespace FracturedJson.Formatting;

/// <summary>
/// A place where strings are piled up sequentially to eventually make one big string, implemented with a
/// good old .NET StringBuilder.
/// </summary>
internal class StringBuilderBuffer : IBuffer
{
    public void Clear()
    {
        _buff.Clear();
    }

    public void Add(string value)
    {
        _buff.Append(value);
    }

    public void Add(params string[] values)
    {
        _buff.AppendJoin(string.Empty, values);
    }

    public void Add(IBuffer valueBuffer)
    {
        if (valueBuffer is StringBuilderBuffer builderBuffer)
            _buff.Append(builderBuffer);
        else
            _buff.Append(valueBuffer.AsString());
    }

    public string AsString()
    {
        return _buff.ToString();
    }

    private readonly StringBuilder _buff = new();
}
