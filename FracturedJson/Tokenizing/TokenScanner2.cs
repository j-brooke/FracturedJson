using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FracturedJson.Tokenizing;

public class TokenScanner
{
    public IEnumerable<JsonToken> Scan(string input)
    {
        var tokenBuffer = new JsonToken[2];
        foreach (var c in input)
        {
            var tokenCount = ProcessChar(c, tokenBuffer);
            for(var i=0; i<tokenCount; ++i)
                yield return tokenBuffer[i];
        }

        var endTokenCount = ProcessEndOfInput(tokenBuffer);
        if (endTokenCount > 0)
            yield return tokenBuffer[0];
    }

    public IEnumerable<JsonToken> Scan(TextReader reader)
    {
        var charBuffer = new char[4096];
        var tokenBuffer = new JsonToken[2];

        int charCount;
        while ((charCount = reader.Read(charBuffer, 0, charBuffer.Length)) > 0)
        {
            for (var i = 0; i < charCount; ++i)
            {
                var tokenCount = ProcessChar(charBuffer[i], tokenBuffer);
                for(var j=0; j<tokenCount; ++j)
                    yield return tokenBuffer[j];
            }
        }

        var endTokenCount = ProcessEndOfInput(tokenBuffer);
        if (endTokenCount > 0)
            yield return tokenBuffer[0];
    }

    /// <summary>
    /// Converts a sequence of characters into a sequence of JSON tokens.  There's no guarantee that the tokens make
    /// sense - just that they're lexically correct.
    /// </summary>
    /// <param name="input">JSON text, with comments</param>
    /// <returns>Enumeration of JsonTokens detailing the token type, textual value, and position in the input.</returns>
    /// <exception cref="FracturedJsonException">Thrown if there's an error parsing tokens.  For instance, if a number
    /// is malformed, or a string isn't terminated, or an unrecognized keyword is encountered.  This method doesn't
    /// concern itself with whether the tokens make sense in the sequence given, such as if braces don't match.
    /// </exception>
    [Obsolete("Use Scan(string) instead.")]
    public static IEnumerable<JsonToken> Scan(IEnumerable<char> input)
    {
        var scanner = new TokenScanner();
        var tokenBuffer = new JsonToken[2];
        foreach (var c in input)
        {
            var tokenCount = scanner.ProcessChar(c, tokenBuffer);
            for(var i=0; i<tokenCount; ++i)
                yield return tokenBuffer[i];
        }

        var endTokenCount = scanner.ProcessEndOfInput(tokenBuffer);
        if (endTokenCount > 0)
            yield return tokenBuffer[0];
    }

    /// <summary>
    /// Reads in a file and returns an enumeration of tokens.
    /// </summary>
    public static IEnumerable<JsonToken> Scan(FileInfo fileInfo)
    {
        var scanner = new TokenScanner();
        using var reader = new StreamReader(fileInfo.FullName);
        return scanner.Scan(reader);
    }

    /// <summary>
    /// Reads in a file and returns an enumeration of characters.
    /// </summary>
    [Obsolete("Use Scan(TextReader) instead.")]
    public static IEnumerable<char> EnumerateFile(FileInfo fileInfo)
    {
        using var reader = new StreamReader(fileInfo.FullName);
        int charRead;
        while ((charRead = reader.Read()) >= 0)
            yield return (char)charRead;
    }

    private enum State
    {
        BetweenTokens,
        InString,
        InStringEscape,
        InStringUnicode0,
        InStringUnicode1,
        InStringUnicode2,
        InStringUnicode3,
        StartingCommentSingleSlash,
        InLineComment,
        InBlockComment,
        InBlockCommentAfterStar,
        KeywordNull1,
        KeywordNull2,
        KeywordNull3,
        KeywordTrue1,
        KeywordTrue2,
        KeywordTrue3,
        KeywordFalse1,
        KeywordFalse2,
        KeywordFalse3,
        KeywordFalse4,
        AfterLf,

        NumberAfterInitialSign,
        NumberAfterInitialZero,
        NumberAfterWholeDigit,
        NumberAfterDecimal,
        NumberAfterFracDigit,
        NumberAfterE,
        NumberAfterExpSign,
        NumberAfterExpDigit,
    }

    private const string _errBadKeyword = "Unexpected keyword";
    private const string _errBadStart = "Invalid character for start of token";
    private const string _errNoCtrl = "Control characters other than whitespace are not allowed";
    private const string _errNoCtrlInString = "Control characters are not allowed in strings";
    private const string _errBadNumberChar = "Bad character while processing number";

    private readonly StringBuilder _buffer = new();
    private InputPosition _currentPosition;
    private InputPosition _tokenPosition;
    private State _state = State.AfterLf;

    /// <summary>
    /// Examine one character and figure out if it starts/continues/finishes a token.  A single character could
    /// produce between zero and two tokens.  That's because we can't actually know if a number is finished until
    /// we see the character after it, which might start a new token or might be a single-character token by itself.
    /// </summary>
    /// <param name="c">The character to process.</param>
    /// <param name="outTokens">Buffer into which tokens are written.  Size must be >=2.</param>
    /// <returns>The count of tokens written outTokens.  (The others will be stale/garbage.)</returns>
    /// <exception cref="FracturedJsonException">If the character wasn't legal according to JSON's lexical rules.
    /// That doesn't guarantee that it makes sense in-context, though.  Two colons in a row are fine for the tokenizer.
    /// </exception>
    private int ProcessChar(char c, JsonToken[] outTokens)
    {
        // Count of how many tokens we've written to outTokens.  Used as a return value, and as an index
        // for where to put the next one.
        var outputCount = 0;

        // In most cases, the signal for the end of a token is a character in the token itself.  In the case of numbers,
        // we don't actually know we've reached the end of a token until we read the character after it.  In that case,
        // out first pass through the loop concludes the number, but then we have to go through again and process
        // that character on its own.  (And it might be a whole token all by itself.)
        while (true)
        {
            switch (_state)
            {
                case State.BetweenTokens:
                    if (c is ' ' or '\t' or '\r')
                        break;
                    if (c == '\n')
                    {
                        _state = State.AfterLf;
                        break;
                    }

                    FracturedJsonException.ThrowIf(char.IsControl(c), _errNoCtrl, _currentPosition);
                    outputCount = StartNewToken(c, outTokens, outputCount);
                    break;
                case State.InString:
                    _buffer.Append(c);

                    switch (c)
                    {
                        case '"':
                            outputCount = FinishTokenBuffer(TokenType.String, outTokens, outputCount);
                            break;
                        case '\\':
                            _state = State.InStringEscape;
                            break;
                        default:
                            FracturedJsonException.ThrowIf(char.IsControl(c), _errNoCtrlInString, _currentPosition);
                            break;
                    }

                    break;
                case State.InStringEscape:
                    _buffer.Append(c);
                    switch (c)
                    {
                        case 'u':
                            _state = State.InStringUnicode0;
                            break;
                        case '"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't':
                            _state = State.InString;
                            break;
                        default:
                            FracturedJsonException.Throw("Bad escaped character in string", _currentPosition);
                            break;
                    }

                    break;
                case State.InStringUnicode0:
                    AcceptHexDigit(c, State.InStringUnicode1);
                    break;
                case State.InStringUnicode1:
                    AcceptHexDigit(c, State.InStringUnicode2);
                    break;
                case State.InStringUnicode2:
                    AcceptHexDigit(c, State.InStringUnicode3);
                    break;
                case State.InStringUnicode3:
                    AcceptHexDigit(c, State.InString);
                    break;
                case State.StartingCommentSingleSlash:
                    _buffer.Append(c);
                    switch (c)
                    {
                        case '/': _state = State.InLineComment; break;
                        case '*': _state = State.InBlockComment; break;
                        default:
                            FracturedJsonException.Throw("Bad character for start of comment", _currentPosition);
                            break;
                    }

                    break;
                case State.InLineComment:
                    _buffer.Append(c);
                    if (c == '\n')
                        outputCount = FinishLineCommentToken(outTokens, outputCount);
                    break;
                case State.InBlockComment:
                    _buffer.Append(c);
                    if (c == '*')
                        _state = State.InBlockCommentAfterStar;
                    break;
                case State.InBlockCommentAfterStar:
                    _buffer.Append(c);
                    switch (c)
                    {
                        case '/':
                            outputCount = FinishTokenBuffer(TokenType.BlockComment, outTokens, outputCount);
                            break;
                        case '*':
                            // No change
                            break;
                        default:
                            _state = State.InBlockComment;
                            break;
                    }
                    break;
                case State.KeywordNull1:
                    FracturedJsonException.ThrowIf(c != 'u', _errBadKeyword, _currentPosition);
                    _state = State.KeywordNull2;
                    break;
                case State.KeywordNull2:
                    FracturedJsonException.ThrowIf(c != 'l', _errBadKeyword, _currentPosition);
                    _state = State.KeywordNull3;
                    break;
                case State.KeywordNull3:
                    FracturedJsonException.ThrowIf(c != 'l', _errBadKeyword, _currentPosition);
                    outputCount = FinishTokenFixed(TokenType.Null, "null", outTokens, outputCount);
                    break;
                case State.KeywordTrue1:
                    FracturedJsonException.ThrowIf(c != 'r', _errBadKeyword, _currentPosition);
                    _state = State.KeywordTrue2;
                    break;
                case State.KeywordTrue2:
                    FracturedJsonException.ThrowIf(c != 'u', _errBadKeyword, _currentPosition);
                    _state = State.KeywordTrue3;
                    break;
                case State.KeywordTrue3:
                    FracturedJsonException.ThrowIf(c != 'e', _errBadKeyword, _currentPosition);
                    outputCount = FinishTokenFixed(TokenType.True, "true", outTokens, outputCount);
                    break;
                case State.KeywordFalse1:
                    FracturedJsonException.ThrowIf(c != 'a', _errBadKeyword, _currentPosition);
                    _state = State.KeywordFalse2;
                    break;
                case State.KeywordFalse2:
                    FracturedJsonException.ThrowIf(c != 'l', _errBadKeyword, _currentPosition);
                    _state = State.KeywordFalse3;
                    break;
                case State.KeywordFalse3:
                    FracturedJsonException.ThrowIf(c != 's', _errBadKeyword, _currentPosition);
                    _state = State.KeywordFalse4;
                    break;
                case State.KeywordFalse4:
                    FracturedJsonException.ThrowIf(c != 'e', _errBadKeyword, _currentPosition);
                    outputCount = FinishTokenFixed(TokenType.False, "false", outTokens, outputCount);
                    break;
                case State.AfterLf:
                    if (c is ' ' or '\t' or '\r')
                        break;
                    if (c == '\n')
                    {
                        outTokens[outputCount] = new JsonToken(TokenType.BlankLine, "\n", _tokenPosition);
                        outputCount += 1;
                        break;
                    }

                    FracturedJsonException.ThrowIf(char.IsControl(c), _errNoCtrl, _currentPosition);
                    outputCount = StartNewToken(c, outTokens, outputCount);
                    break;
                case State.NumberAfterInitialSign:
                case State.NumberAfterInitialZero:
                case State.NumberAfterWholeDigit:
                case State.NumberAfterDecimal:
                case State.NumberAfterFracDigit:
                case State.NumberAfterE:
                case State.NumberAfterExpSign:
                case State.NumberAfterExpDigit:
                    // Delegate the number handling to another function.
                    var endOfNumber = AcceptNumber(c);
                    if (endOfNumber)
                    {
                        outputCount = FinishTokenBuffer(TokenType.Number, outTokens, outputCount);

                        // If this char triggered the end of a number, then it is actually the character after
                        // the number token.  We need to go back to the top of the loop and look at it again.
                        // (This can only happen once per call.)
                        continue;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            FracturedJsonException.ThrowIf(_currentPosition.Index == int.MaxValue,
                "Maximum document length exceeded", _currentPosition);

            // Update position and exit the loop and function.  We've done everything we need to with this char.
            _currentPosition = (c == '\n')
                ? new InputPosition(_currentPosition.Index + 1, _currentPosition.Row + 1, 0)
                : new InputPosition(_currentPosition.Index + 1, _currentPosition.Row, _currentPosition.Column + 1);

            // If this is the start of a new line, mark the location of the beginning of the line.  We'll use it
            // as the location if we emit a BlankLine token, even if there's whitespace.
            if (c == '\n' && (_state is State.AfterLf or State.BetweenTokens))
                _tokenPosition = _currentPosition;

            return outputCount;
        }
    }

    /// <summary>
    /// Starts a new token from the given char at the current position, or throws if it's not a valid starting char.
    /// If the char represents a single-character token other than zero, it is immediately placed into outTokens.
    /// </summary>
    /// <param name="c">Character meant to start a new token.</param>
    /// <param name="outTokens">Storage buffer for returning generated tokens to caller.</param>
    /// <param name="outputCount">Number of tokens written to outTokens before this call.</param>
    /// <returns>The number of tokens written to outTokens after this call.</returns>
    /// <exception cref="FracturedJsonException">If the character cannot be the start of a token.</exception>
    private int StartNewToken(char c, Span<JsonToken> outTokens, int outputCount)
    {
        _tokenPosition = _currentPosition;

        // If it's a one-character token, don't bother with buffers and such - just emit the token and move on.
        _state = State.BetweenTokens;
        switch (c)
        {
            case '[':
                outTokens[outputCount] = new JsonToken(TokenType.BeginArray, "[", _tokenPosition);
                return 1 + outputCount;
            case ']':
                outTokens[outputCount] = new JsonToken(TokenType.EndArray, "]", _tokenPosition);
                return 1 + outputCount;
            case '{':
                outTokens[outputCount] = new JsonToken(TokenType.BeginObject, "{", _tokenPosition);
                return 1 + outputCount;
            case '}':
                outTokens[outputCount] = new JsonToken(TokenType.EndObject, "}", _tokenPosition);
                return 1 + outputCount;
            case ':':
                outTokens[outputCount] = new JsonToken(TokenType.Colon, ":", _tokenPosition);
                return 1 + outputCount;
            case ',':
                outTokens[outputCount] = new JsonToken(TokenType.Comma, ",", _tokenPosition);
                return 1 + outputCount;
        }

        // The character is either invalid - in which case we'll throw an exception - or this is the start of a
        // potentially multi-character token.  (The only case where it's only one character is the digit zero,
        // but we can't know that it's a single character yet.)  Set the starting token position and reset the
        // buffer.
        _buffer.Clear().Append(c);

        switch (c)
        {
            case '/': _state = State.StartingCommentSingleSlash; break;
            case '"': _state = State.InString; break;
            case 't': _state = State.KeywordTrue1; break;
            case 'f': _state = State.KeywordFalse1; break;
            case 'n': _state = State.KeywordNull1; break;

            case '-': _state = State.NumberAfterInitialSign; break;
            case '0': _state = State.NumberAfterInitialZero; break;
            case '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9':
                _state = State.NumberAfterWholeDigit; break;

            default:
                FracturedJsonException.Throw(_errBadStart, _currentPosition);
                break;
        }

        return outputCount;
    }

    /// <summary>
    /// Writes the completed token to outTokens using a fixed string for the token text.
    /// </summary>
    /// <param name="type">Type of the completed token</param>
    /// <param name="tokenText">Text of the completed token</param>
    /// <param name="outTokens">Buffer to put the token into</param>
    /// <param name="outputCount">Number of tokens written to outTokens before this call.</param>
    /// <returns>The number of tokens written to outTokens after this call.</returns>
    private int FinishTokenFixed(TokenType type, string tokenText, JsonToken[] outTokens, int outputCount)
    {
        outTokens[outputCount] = new JsonToken(type, tokenText, _tokenPosition);
        _state = State.BetweenTokens;
        return 1 + outputCount;
    }

    /// <summary>
    /// Writes the completed token to outTokens using the buffered characters for the token text.
    /// </summary>
    /// <param name="type">Type of the completed token</param>
    /// <param name="outTokens">Buffer to put the token into</param>
    /// <param name="outputCount">Number of tokens written to outTokens before this call.</param>
    /// <returns>The number of tokens written to outTokens after this call.</returns>
    private int FinishTokenBuffer(TokenType type, JsonToken[] outTokens, int outputCount)
    {
        outTokens[outputCount] = new JsonToken(type, _buffer.ToString(), _tokenPosition);
        _state = State.BetweenTokens;
        return 1 + outputCount;
    }

    private int FinishLineCommentToken(JsonToken[] outTokens, int outputCount)
    {
        outTokens[outputCount] = new JsonToken(TokenType.LineComment, _buffer.ToString().TrimEnd(), _tokenPosition);
        _state = State.AfterLf;
        return 1 + outputCount;
    }



    /// <summary>
    /// Adds the character to the buffer and changes the state, as long as c is a hex digit.
    /// </summary>
    /// <param name="c">The character to process</param>
    /// <param name="nextState">State to change to</param>
    /// <exception cref="FracturedJsonException">If c is not a hex digit.</exception>
    private void AcceptHexDigit(char c, State nextState)
    {
        FracturedJsonException.ThrowIf(!Uri.IsHexDigit(c), "Bad unicode escape in string", _currentPosition);
        _buffer.Append(c);
        _state = nextState;
    }

    /// <summary>
    /// Adds the character to the buffer and changes state as long as it's in one of the number states already and
    /// the character is valid.  Throws if it's just plain invalid, like anything except a digit after a decimal point.
    /// </summary>
    /// <param name="c">The character to process.</param>
    /// <returns>True if the token should be finished and the character reprocessed.</returns>
    /// <exception cref="FracturedJsonException">If c is not valid as part of the number and can't be the start of a
    /// new token.</exception>
    private bool AcceptNumber(char c)
    {
        switch (_state)
        {
            case State.NumberAfterInitialSign:
                if (c == '0')
                    _state = State.NumberAfterInitialZero;
                else if (char.IsDigit(c))
                    _state = State.NumberAfterWholeDigit;
                else
                    FracturedJsonException.Throw(_errBadNumberChar, _currentPosition);
                break;
            case State.NumberAfterInitialZero:
                if (c == '0')
                    FracturedJsonException.Throw(_errBadNumberChar, _currentPosition);
                else if (char.IsDigit(c))
                    _state = State.NumberAfterWholeDigit;
                else if (c == '.')
                    _state = State.NumberAfterDecimal;
                else if (c is 'e' or 'E')
                    _state = State.NumberAfterE;
                else
                    return true;
                break;
            case State.NumberAfterWholeDigit:
                if (char.IsDigit(c))
                    break;
                if (c == '.')
                    _state = State.NumberAfterDecimal;
                else if (c is 'e' or 'E')
                    _state = State.NumberAfterE;
                else
                    return true;
                break;
            case State.NumberAfterDecimal:
                if (!char.IsDigit(c))
                    FracturedJsonException.Throw(_errBadNumberChar, _currentPosition);
                _state = State.NumberAfterFracDigit;
                break;
            case State.NumberAfterFracDigit:
                if (char.IsDigit(c))
                    break;
                if (c is 'e' or 'E')
                    _state = State.NumberAfterE;
                else
                    return true;
                break;
            case State.NumberAfterE:
                if (char.IsDigit(c))
                    _state = State.NumberAfterExpDigit;
                else if (c is '+' or '-')
                    _state = State.NumberAfterExpSign;
                else
                    FracturedJsonException.Throw(_errBadNumberChar, _currentPosition);
                break;
            case State.NumberAfterExpSign:
                if (!char.IsDigit(c))
                    FracturedJsonException.Throw(_errBadNumberChar, _currentPosition);
                _state = State.NumberAfterExpDigit;
                break;
            case State.NumberAfterExpDigit:
                if (!char.IsDigit(c))
                    return true;
                break;
            default:
                FracturedJsonException.Throw("Logic error tokenizing number", _currentPosition);
                break;
        }

        _buffer.Append(c);
        return false;
    }

    private int ProcessEndOfInput(JsonToken[] outTokens)
    {
        var outputCount = 0;
        switch (_state)
        {
            case State.BetweenTokens:
            case State.AfterLf:
                break;
            case State.InString:
            case State.InStringEscape:
            case State.InStringUnicode0:
            case State.InStringUnicode1:
            case State.InStringUnicode2:
            case State.InStringUnicode3:
                FracturedJsonException.Throw("Unexpected end of input while processing string",  _currentPosition);
                break;
            case State.InLineComment:
                // End of input inside a line comment is cool - just finish the token.
                outputCount = FinishTokenBuffer(TokenType.LineComment, outTokens, outputCount);
                break;
            case State.StartingCommentSingleSlash:
            case State.InBlockComment:
            case State.InBlockCommentAfterStar:
                FracturedJsonException.Throw("Unexpected end of input while processing comment",  _currentPosition);
                break;
            case State.KeywordNull1:
            case State.KeywordNull2:
            case State.KeywordNull3:
            case State.KeywordTrue1:
            case State.KeywordTrue2:
            case State.KeywordTrue3:
            case State.KeywordFalse1:
            case State.KeywordFalse2:
            case State.KeywordFalse3:
            case State.KeywordFalse4:
                FracturedJsonException.Throw("Unexpected end of input while processing keyword", _currentPosition);
                break;
            case State.NumberAfterInitialZero:
            case State.NumberAfterWholeDigit:
            case State.NumberAfterFracDigit:
            case State.NumberAfterExpDigit:
                // A number was valid if we ended in these number states.  Finish it.
                outputCount = FinishTokenBuffer(TokenType.Number, outTokens, outputCount);
                break;
            case State.NumberAfterInitialSign:
            case State.NumberAfterDecimal:
            case State.NumberAfterE:
            case State.NumberAfterExpSign:
                // If we ended in any of these number states, the number isn't valid.
                FracturedJsonException.Throw("Unexpected end of input while processing number", _currentPosition);
                break;
        }

        return outputCount;
    }
}
