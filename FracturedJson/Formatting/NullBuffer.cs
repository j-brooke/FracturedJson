namespace FracturedJson.Formatting;

/// <summary>
/// A do-nothing IBuffer, just to avoid null reference exceptions and such.
/// </summary>
public class NullBuffer : IBuffer
{
    public IBuffer Add(string value)
    {
        return this;
    }

    public IBuffer Add(params string[] values)
    {
        return this;
    }

    public IBuffer EndLine(string eolString)
    {
        return this;
    }

    public IBuffer Flush()
    {
        return this;
    }
}
