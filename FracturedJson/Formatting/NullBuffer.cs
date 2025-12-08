namespace FracturedJson.Formatting;

/// <summary>
/// A do-nothing IBuffer, just to avoid null reference exceptions and such.
/// </summary>
public class NullBuffer : IBuffer
{
    /// <summary>
    /// Add a single string to the buffer.
    /// </summary>
    public IBuffer Add(string value)
    {
        return this;
    }

    /// <summary>
    /// Add a group of strings to the buffer.
    /// </summary>
    public IBuffer Add(params string[] values)
    {
        return this;
    }

    /// <summary>
    /// Adds the requested number of spaces to the buffer.
    /// </summary>
    public IBuffer Spaces(int count)
    {
        return this;
    }

    /// <summary>
    /// Call this only when sending an end-of-line symbol to the buffer.  Doing so helps the buffer with
    /// extra post-processing, like trimming trailing whitespace.
    /// </summary>
    public IBuffer EndLine(string eolString)
    {
        return this;
    }

    /// <summary>
    /// Call this to let the buffer finish up any work in progress.
    /// </summary>
    public IBuffer Flush()
    {
        return this;
    }
}
