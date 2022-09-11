using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FracturedJson.Tokenizer;

public class ScannerState
{
    public StringBuilder Buffer { get; } = new();
    public InputPosition CurrentPosition { get; private set; }
    public InputPosition TokenPosition { get; private set; }
    public bool NonWhitespaceSinceLastNewline { get; private set; }

    public void Advance(bool isWhitespace)
    {
        CurrentPosition = new(CurrentPosition.Index + 1, CurrentPosition.Row, CurrentPosition.Column + 1);
        NonWhitespaceSinceLastNewline |= !isWhitespace;
    }

    public void NewLine()
    {
        CurrentPosition = new(CurrentPosition.Index + 1, CurrentPosition.Row + 1, 0);
        NonWhitespaceSinceLastNewline = false;
    }

    public void SetTokenStart()
    {
        TokenPosition = CurrentPosition;
    }

    public JsonToken MakeTokenFromBuffer(TokenType type, bool trimEnd = false)
    {
        var text = (trimEnd) ? Buffer.ToString().TrimEnd() : Buffer.ToString();
        return new JsonToken(type, text, TokenPosition);
    }

    public JsonToken MakeToken(TokenType type, string text)
    {
        return new JsonToken(type, text, TokenPosition);
    }

    [DoesNotReturn]
    public void Throw(string message)
    {
        throw new FracturedJsonException(message, CurrentPosition);
    }
}
