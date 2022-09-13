using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FracturedJson.Tokenizer;

public static class TokenScanner
{
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
    public static IEnumerable<JsonToken> Scan(IEnumerable<char> input)
    {
        var state = new ScannerState();
        using var enumerator = input.GetEnumerator();
        
        if (!enumerator.MoveNext())
            yield break;
        
        while (true)
        {
            // In most cases we consume the character that tells us we've reached the end of a token, but sometimes
            // we need to examine a character that ends up belonging to the *next* token.  In that case, we set
            // lookedAhead to true.
            var lookedAhead = false;
            
            var ch = enumerator.Current;
            if (ch == ' ' || ch == '\t' || ch == '\r')
            {
                // Regular unremarkable whitespace.
                state.Advance(true);
            }
            else if (ch == '\n')
            {
                // If a line contained only whitespace, return a blank line.  Note that we're ignoring CRs.  If
                // we get a Window's style CRLF, we throw away the CR, and then trigger on the LF just like we would
                // for Unix.
                if (!state.NonWhitespaceSinceLastNewline)
                    yield return state.MakeToken(TokenType.BlankLine, "\n");
                
                state.NewLine();
                
                // If this new line turns out to be nothing but whitespace, we want to report the blank line
                // token as starting at the beginning of the line.  Otherwise you get into \r\n vs. \n issues.
                state.SetTokenStart();
            }
            else
            {
                // Any other character is either the start of a new token, or an error.
                state.SetTokenStart();
                yield return ch switch
                {
                    '{' => ProcessSingleChar(state, "{", TokenType.BeginObject),
                    '}' => ProcessSingleChar(state, "}", TokenType.EndObject),
                    '[' => ProcessSingleChar(state, "[", TokenType.BeginArray),
                    ']' => ProcessSingleChar(state, "]", TokenType.EndArray),
                    ':' => ProcessSingleChar(state, ":", TokenType.Colon),
                    ',' => ProcessSingleChar(state, ",", TokenType.Comma),
                    't' => ProcessKeyword(state, enumerator, "true", TokenType.True),
                    'f' => ProcessKeyword(state, enumerator, "false", TokenType.False),
                    'n' => ProcessKeyword(state, enumerator, "null", TokenType.Null),
                    '/' => ProcessComment(state, enumerator),
                    '"' => ProcessString(state, enumerator),
                    '-' => ProcessNumber(state, enumerator, out lookedAhead),
                    _ when char.IsDigit(ch) => ProcessNumber(state, enumerator, out lookedAhead),
                    _ => throw new FracturedJsonException("Unexpected character", state.CurrentPosition)
                };                
            }
            
            // The enumerator might be pointed at the current character, that we consumed as part of an already-returned
            // token, or it might be pointed to the next character which might be the start of a new token.
            if (!lookedAhead)
            {
                // Advance the enumerator.  If there's nothing left, we're finished.
                if (!enumerator.MoveNext())
                    yield break;
            }
        }
    }

    /// <summary>
    /// Read the enumerator's current character as a single-character token. (Like [, {, :, ], etc.)
    /// </summary>
    private static JsonToken ProcessSingleChar(ScannerState state, string symbol, TokenType type)
    {
        state.Advance(false);
        return state.MakeToken(type, symbol);
    }

    /// <summary>
    /// Reads a specific known word, or throws if we can't read the whole thing.  Assumes that the enumerator's current
    /// character is on the first character of the word.
    /// </summary>
    private static JsonToken ProcessKeyword(ScannerState state, IEnumerator<char> enumerator, string keyword, TokenType type)
    {
        // I know this loop structure is weird.  It's because we start with the enumerator already on the first
        // character, but we want to end with it on this word's final character, not the one after it.
        var charIndex = 0;
        while (true)
        {
            if (keyword[charIndex] != enumerator.Current)
                state.Throw("Unexpected keyword");
            state.Advance(false);
            charIndex += 1;

            if (charIndex == keyword.Length)
                return state.MakeToken(type, keyword);
            if (!enumerator.MoveNext())
                state.Throw("Unexpected end of input while processing keyword");
        }
    }

    /// <summary>
    /// Reads to the end of a block or line comment.  Assumes the enumerator is pointed to the initial slash.
    /// If it's a line comment, any trailing whitespace isn't included in the returned token text.  Internal
    /// whitespace is preserved, including possibly line breaks if it's a block comment.  (And those could be
    /// CRLF or LF, depending on the input.)
    /// </summary>
    private static JsonToken ProcessComment(ScannerState state, IEnumerator<char> enumerator)
    {
        state.Buffer.Clear();
        state.Buffer.Append(enumerator.Current);
        state.Advance(false);

        if (!enumerator.MoveNext())
            state.Throw("Unexpected end of input while processing comment");

        var isBlockComment = false;
        if (enumerator.Current == '*')
            isBlockComment = true;
        else if (enumerator.Current != '/')
            state.Throw("Bad character for start of comment");
        
        state.Buffer.Append(enumerator.Current);
        state.Advance(false);

        var lastCharWasAsterisk = false;
        while (true)
        {
            if (!enumerator.MoveNext())
            {
                // If the input ends while we're in the middle of a block comment, treat it as an error.  If it
                // ends in the middle of a line comment, treat the comment as valid.
                if (isBlockComment)
                    state.Throw("Unexpected end of input while processing comment");
                else
                    return state.MakeTokenFromBuffer(TokenType.LineComment, true);
            }            
            var ch = enumerator.Current;

            if (ch == '\n')
            {
                state.NewLine();
                if (!isBlockComment)
                    return state.MakeTokenFromBuffer(TokenType.LineComment, true);
                
                state.Buffer.Append(ch);
                continue;
            }
            
            state.Buffer.Append(ch);
            state.Advance(false);
            
            if (ch == '/' && lastCharWasAsterisk)
                return state.MakeTokenFromBuffer(TokenType.BlockComment);
            lastCharWasAsterisk = (ch == '*');
        }
    }

    /// <summary>
    /// Process a string.  Assumes that the enumerator is pointing at the initial quote character, and will end with
    /// it pointing to the closing quote character (assuming it doesn't throw an exception).
    /// </summary>
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    private static JsonToken ProcessString(ScannerState state, IEnumerator<char> enumerator)
    {
        const string legalAfterBackslash = "\"\\/bfnrtu";
        
        state.Buffer.Clear();
        state.Buffer.Append(enumerator.Current);
        state.Advance(false);
        
        var lastCharBeganEscape = false;
        var expectedHexCount = 0;
        while (true)
        {
            if (!enumerator.MoveNext())
                state.Throw("Unexpected end of input while processing string"); 
            var ch = enumerator.Current;
            state.Buffer.Append(enumerator.Current);

            // If we previously read \u, it must be followed by exactly 4 hex digits.
            if (expectedHexCount > 0)
            {
                if (!Uri.IsHexDigit(ch))
                    state.Throw("Bad unicode escape in string");
                expectedHexCount -= 1;
                state.Advance(false);
                continue;
            }

            // Only certain characters are allowed after backslashes.  The only ones that affect us here are 
            // \u, which needs to be followed by 4 hex digits, and \", which should not end the string.
            if (lastCharBeganEscape)
            {
                if (!legalAfterBackslash.Contains(ch))
                    state.Throw("Bad escaped character in string");
                if (ch == 'u')
                    expectedHexCount = 4;
                lastCharBeganEscape = false;
                state.Advance(false);
                continue;
            }
            
            if (char.IsControl(ch))
                state.Throw("Control characters are not allowed in strings");

            state.Advance(false);
            
            if (ch == '"')
                return state.MakeTokenFromBuffer(TokenType.String);
            if (ch == '\\')
                lastCharBeganEscape = true;
        }
    }

    /// <summary>
    /// Processes a number.  The enumerator is assumed to be pointed to the initial digit or - sign.  On exit the
    /// enumerator might be pointing at the character after the number, or it might have reached the end of the input.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="enumerator"></param>
    /// <param name="lookedAhead">true if the enumerator is left pointed to the character *after* the last character
    /// of this number.</param>
    /// <returns></returns>
    private static JsonToken ProcessNumber(ScannerState state, IEnumerator<char> enumerator, out bool lookedAhead)
    {
        state.Buffer.Clear();
        var phase = NumberPhase.Beginning;

        while (true)
        {
            // Simple state machine.  We know that enumerator.Current is valid at this point.
            var ch = enumerator.Current;
            var handling = CharHandling.ValidAndConsumed;
            switch (phase)
            {
                case NumberPhase.Beginning:
                    if (ch == '-')
                        phase = NumberPhase.PastLeadingSign;
                    else if (ch == '0')
                        phase = NumberPhase.PastWhole;
                    else if (char.IsDigit(ch))
                        phase = NumberPhase.PastFirstDigitOfWhole;
                    else
                        handling = CharHandling.InvalidatesToken;
                    break;
                
                case NumberPhase.PastLeadingSign:
                    if (!char.IsDigit(ch))
                        handling = CharHandling.InvalidatesToken;
                    else if (ch == '0')
                        phase = NumberPhase.PastWhole;
                    else
                        phase = NumberPhase.PastFirstDigitOfWhole;
                    break;
                
                // We've started with a 1-9 and more digits are welcome.
                case NumberPhase.PastFirstDigitOfWhole:
                    if (ch == '.')
                        phase = NumberPhase.PastDecimalPoint;
                    else if (ch == 'e' || ch == 'E')
                        phase = NumberPhase.PastE;
                    else if (!char.IsDigit(ch))
                        handling = CharHandling.StartOfNewToken;
                    break;
                
                // We started with a 0.  Another digit at this point would not be part of this token.
                case NumberPhase.PastWhole:
                    if (ch == '.')
                        phase = NumberPhase.PastDecimalPoint;
                    else if (ch == 'e' || ch == 'E')
                        phase = NumberPhase.PastE;
                    else
                        handling = CharHandling.StartOfNewToken;
                    break;

                // A decimal point must be followed by a digit.
                case NumberPhase.PastDecimalPoint:
                    if (char.IsDigit(ch))
                        phase = NumberPhase.PastFirstDigitOfFractional;
                    else
                        handling = CharHandling.InvalidatesToken;
                    break;
                
                case NumberPhase.PastFirstDigitOfFractional:
                    if (ch == 'e' || ch == 'E')
                        phase = NumberPhase.PastE;
                    else if (!char.IsDigit(ch))
                        handling = CharHandling.StartOfNewToken;
                    break;
                
                // An E must be followed by either a digit or +/-
                case NumberPhase.PastE:
                    if (ch == '+' || ch == '-')
                        phase = NumberPhase.PastExpSign;
                    else if (char.IsDigit(ch))
                        phase = NumberPhase.PastFirstDigitOfExponent;
                    else
                        handling = CharHandling.InvalidatesToken;
                    break;
                
                // E and a +/- must still be followed by one or more digits.
                case NumberPhase.PastExpSign:
                    if (char.IsDigit(ch))
                        phase = NumberPhase.PastFirstDigitOfExponent;
                    else
                        handling = CharHandling.InvalidatesToken;
                    break;
                
                case NumberPhase.PastFirstDigitOfExponent:
                    if (!char.IsDigit(ch))
                        handling = CharHandling.StartOfNewToken;
                    break;
            }
            
            if (handling==CharHandling.InvalidatesToken)
                state.Throw("Bad character while processing number");
            
            if (handling == CharHandling.StartOfNewToken)
            {
                // We're done processing the number, and the enumerator is pointed to the character after it.
                lookedAhead = true;
                return state.MakeTokenFromBuffer(TokenType.Number);
            }
            
            state.Buffer.Append(enumerator.Current);
            state.Advance(false);

            if (enumerator.MoveNext())
                continue;
            
            // We've reached the end of the input.  The number token may or may not be valid, depending on where
            // we were.
            lookedAhead = false;
            switch (phase)
            {
                case NumberPhase.PastFirstDigitOfWhole:
                case NumberPhase.PastWhole:
                case NumberPhase.PastFirstDigitOfFractional:
                case NumberPhase.PastFirstDigitOfExponent:
                    return state.MakeTokenFromBuffer(TokenType.Number);
                default:
                    state.Throw("Unexpected end of input while processing number");
                    break;
            }
        }
    }

    private enum NumberPhase
    {
        Beginning,
        PastLeadingSign,
        PastFirstDigitOfWhole,
        PastWhole,
        PastDecimalPoint,
        PastFirstDigitOfFractional,
        PastE,
        PastExpSign,
        PastFirstDigitOfExponent,
    }

    private enum CharHandling
    {
        InvalidatesToken,
        ValidAndConsumed,
        StartOfNewToken,
    }
}
