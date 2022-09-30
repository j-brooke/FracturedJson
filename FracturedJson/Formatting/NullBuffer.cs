namespace FracturedJson.Formatting;

public class NullBuffer : IBuffer
{
    public void Add(string value)
    {
    }

    public void Add(params string[] values)
    {
    }

    public string AsString()
    {
        return string.Empty;
    }
}