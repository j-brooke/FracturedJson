using System.Text;

namespace FracturedJson.Formatting;

/// <summary>
/// A place where strings are piled up sequentially to eventually make one big string, implemented with a
/// good old .NET StringBuilder.
/// </summary>
public class StringBuilderBuffer : IBuffer
{
    public void Add(string value)
    {
        _buff.Append(value);
    }

    public void Add(params string[] values)
    {
        foreach (var val in values)
            _buff.Append(val);
    }

    public string AsString()
    {
        return _buff.ToString();
    }

    private readonly StringBuilder _buff = new();
}
