namespace FracturedJson.Tokenizer;

/// <summary>
/// A piece of JSON text that makes sense to treat as a whole thing when analyzing a document's structure.
/// For example, a string is a token, regardless of whether it represents a value or an object key.
/// </summary>
public record struct JsonToken(TokenType Type, string Text, InputPosition InputPosition)
{
    public TokenType Type { get; } = Type;
    public string Text { get; } = Text;
    public InputPosition InputPosition { get; } = InputPosition;
}
