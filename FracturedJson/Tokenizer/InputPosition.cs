namespace FracturedJson.Tokenizer;

/// <summary>
/// Structure representing a location in an input stream.
/// </summary>
/// <param name="Index">Number of characters from the start of the input.</param>
/// <param name="Row">Number of newlines since the start of the input.</param>
/// <param name="Column">Number of characters since the latest newline.</param>
public readonly record struct InputPosition(long Index, long Row, long Column)
{
    public long Index { get; } = Index;
    public long Row { get; } = Row;
    public long Column { get; } = Column;
}
