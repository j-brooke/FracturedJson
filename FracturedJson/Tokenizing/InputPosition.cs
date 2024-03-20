namespace FracturedJson.Tokenizing;

/// <summary>
/// Structure representing a location in an input stream.
/// </summary>
/// <param name="Index">Number of characters from the start of the input.</param>
/// <param name="Row">Number of newlines since the start of the input.</param>
/// <param name="Column">Number of characters since the latest newline.</param>
public readonly record struct InputPosition(int Index, int Row, int Column)
{
    /// <summary>
    /// Number of characters from the start of the input.
    /// </summary>
    public int Index { get; } = Index;

    /// <summary>
    /// Number of newlines since the start of the input.
    /// </summary>
    public int Row { get; } = Row;

    /// <summary>
    /// Number of characters since the latest newline.
    /// </summary>
    public int Column { get; } = Column;
}
