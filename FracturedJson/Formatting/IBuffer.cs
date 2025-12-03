namespace FracturedJson.Formatting;

/// <summary>
/// A place where strings are piled up sequentially to eventually make one big string.  Or maybe straight to a
/// stream or whatever.
/// </summary>
public interface IBuffer
{
    /// <summary>
    /// Add a single string to the buffer.
    /// </summary>
    public IBuffer Add(string value);

    /// <summary>
    /// Add a group of strings to the buffer.
    /// </summary>
    public IBuffer Add(params string[] values);

    public IBuffer Spaces(int count);

    /// <summary>
    /// Call this only when sending an end-of-line symbol to the buffer.  Doing so helps the buffer with
    /// extra post-processing, like trimming trailing whitespace.
    /// </summary>
    public IBuffer EndLine(string eolString);

    /// <summary>
    /// Call this to let the buffer finish up any work in progress.
    /// </summary>
    public IBuffer Flush();
}
