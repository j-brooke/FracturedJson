namespace FracturedJson.Formatting;

/// <summary>
/// A place where strings are piled up sequentially to eventually make one big string.  Or maybe straight to a
/// stream or whatever.
/// </summary>
public interface IBuffer
{
    public void Add(string value);
    public void Add(params string[] values);
}
