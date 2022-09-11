namespace FracturedJson.Tokenizer;

public readonly record struct InputPosition(long Index, long Row, long Column)
{
    public long Index { get; } = Index;
    public long Row { get; } = Row;
    public long Column { get; } = Column;
}
