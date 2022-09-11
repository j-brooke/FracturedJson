namespace FracturedJson.Tokenizer;

public record struct JsonToken(TokenType Type, string Text, InputPosition InputPosition)
{
    public TokenType Type { get; } = Type;
    public string Text { get; } = Text;
    public InputPosition InputPosition { get; } = InputPosition;
}
