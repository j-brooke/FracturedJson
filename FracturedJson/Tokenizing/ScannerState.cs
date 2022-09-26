using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FracturedJson.Tokenizing;

/// <summary>
/// Class for keeping track of info while scanning text into JSON tokens.
/// </summary>
internal class ScannerState
{
    public StringBuilder Buffer { get; } = new();
    public InputPosition CurrentPosition { get; private set; }
    public InputPosition TokenPosition { get; private set; }
    public bool NonWhitespaceSinceLastNewline { get; private set; }

    /// <summary>
    /// Moves the current position to the right by one.
    /// </summary>
    /// <param name="isWhitespace">True if the character just read was whitespace outside of a token.</param>
    public void Advance(bool isWhitespace)
    {
        try
        {
            checked
            {
                CurrentPosition = new(CurrentPosition.Index + 1, CurrentPosition.Row, CurrentPosition.Column + 1);
                NonWhitespaceSinceLastNewline |= !isWhitespace;
            }
        }
        catch (OverflowException e)
        {
            throw new FracturedJsonException("Maximum document length exceeded", e, CurrentPosition);
        }
    }

    /// <summary>
    /// Moves the current position to the start of a new line.
    /// </summary>
    public void NewLine()
    {
        try
        {
            checked
            {
                CurrentPosition = new(CurrentPosition.Index + 1, CurrentPosition.Row + 1, 0);
                NonWhitespaceSinceLastNewline = false;
            }
        }
        catch (OverflowException e)
        {
            throw new FracturedJsonException("Maximum document length exceeded", e, CurrentPosition);
        }
    }

    /// <summary>
    /// Sets TokenPosition from CurrentPosition.
    /// </summary>
    public void SetTokenStart()
    {
        TokenPosition = CurrentPosition;
    }

    /// <summary>
    /// Creates a new JsonToken with text from Buffer and InputPosition from TokenPosition.
    /// </summary>
    public JsonToken MakeTokenFromBuffer(TokenType type, bool trimEnd = false)
    {
        var text = (trimEnd) ? Buffer.ToString().TrimEnd() : Buffer.ToString();
        return new JsonToken(type, text, TokenPosition);
    }

    /// <summary>
    /// Creates a new JsonToken using the supplied text, with InputPosition set from TokenPosition.
    /// </summary>
    public JsonToken MakeToken(TokenType type, string text)
    {
        return new JsonToken(type, text, TokenPosition);
    }

    /// <summary>
    /// Throws a FracturedJsonException noting the current input position.
    /// </summary>
    [DoesNotReturn]
    public void Throw(string message)
    {
        throw FracturedJsonException.Create(message, CurrentPosition);
    }
}
