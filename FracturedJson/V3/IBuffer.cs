namespace FracturedJson.V3;

/// <summary>
/// A place where strings are piled up sequentially to eventually make one big string.
/// </summary>
public interface IBuffer
{
    public void Clear();
    public void Add(string value);
    public void Add(params string[] values);
    public void Add(IBuffer valueBuffer);
    public string AsString();
}
