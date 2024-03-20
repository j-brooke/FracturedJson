namespace FracturedJson.Tokenizing;

/// <summary>
/// A piece of JSON text that makes sense to treat as a whole thing when analyzing a document's structure.
/// For example, a string is a token, regardless of whether it represents a value or an object key.
/// </summary>
public record struct JsonToken(TokenType Type, string Text, InputPosition InputPosition)
{
    /// <summary>
    /// What sort of JSON thing this is.
    /// </summary>
    public TokenType Type { get; } = Type;

    /// <summary>
    /// The text that makes up this token from the original input.
    /// </summary>
    public string Text { get; } = Text;

    /// <summary>
    /// Location of the start of this token in the input.
    /// </summary>
    public InputPosition InputPosition { get; } = InputPosition;
}
