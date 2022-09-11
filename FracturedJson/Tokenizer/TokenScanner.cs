using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FracturedJson.Tokenizer;

public static class TokenScanner
{
    public static IEnumerable<JsonToken> Scan(IEnumerable<char> input)
    {
        var state = new ScannerState();
        using var enumerator = input.GetEnumerator();
        
        if (!enumerator.MoveNext())
            yield break;
        
        while (true)
        {
            var lookedAhead = false;
            var ch = enumerator.Current;
            if (ch == ' ' || ch == '\t' || ch == '\r')
            {
                state.Advance(true);
            }
            else if (ch == '\n')
            {
                if (!state.NonWhitespaceSinceLastNewline)
                    yield return state.MakeToken(TokenType.BlankLine, "\n");
                
                state.NewLine();
                
                // If this line turns out to be nothing but whitespace, we want to report the blank line
                // token as starting at the beginning of the line.  Otherwise you get into \r\n vs. \n issues.
                state.SetTokenStart();
            }
            else
            {
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
            
            if (!lookedAhead)
            {
                if (!enumerator.MoveNext())
                    yield break;
            }
        }
    }

    private static JsonToken ProcessSingleChar(ScannerState state, string symbol, TokenType type)
    {
        state.Advance(false);
        return state.MakeToken(type, symbol);
    }

    private static JsonToken ProcessKeyword(ScannerState state, IEnumerator<char> enumerator, string keyword, TokenType type)
    {
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

            if (expectedHexCount > 0)
            {
                if (!Uri.IsHexDigit(ch))
                    state.Throw("Bad unicode escape in string");
                expectedHexCount -= 1;
                state.Advance(false);
                continue;
            }

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

    private static JsonToken ProcessNumber(ScannerState state, IEnumerator<char> enumerator, out bool lookedAhead)
    {
        state.Buffer.Clear();
        var phase = NumberPhase.Beginning;

        while (true)
        {
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
                        phase = NumberPhase.PastFirstWhole;
                    else
                        handling = CharHandling.InvalidatesToken;
                    break;
                
                case NumberPhase.PastLeadingSign:
                    if (!char.IsDigit(ch))
                        handling = CharHandling.InvalidatesToken;
                    else if (ch == '0')
                        phase = NumberPhase.PastWhole;
                    else
                        phase = NumberPhase.PastFirstWhole;
                    break;
                
                case NumberPhase.PastFirstWhole:
                    if (ch == '.')
                        phase = NumberPhase.PastDecimalPoint;
                    else if (ch == 'e' || ch == 'E')
                        phase = NumberPhase.PastE;
                    else if (!char.IsDigit(ch))
                        handling = CharHandling.StartOfNewToken;
                    break;
                
                case NumberPhase.PastWhole:
                    if (ch == '.')
                        phase = NumberPhase.PastDecimalPoint;
                    else if (ch == 'e' || ch == 'E')
                        phase = NumberPhase.PastE;
                    else
                        handling = CharHandling.StartOfNewToken;
                    break;

                case NumberPhase.PastDecimalPoint:
                    if (char.IsDigit(ch))
                        phase = NumberPhase.PastFirstFractional;
                    else
                        handling = CharHandling.InvalidatesToken;
                    break;
                
                case NumberPhase.PastFirstFractional:
                    if (ch == 'e' || ch == 'E')
                        phase = NumberPhase.PastE;
                    else if (!char.IsDigit(ch))
                        handling = CharHandling.StartOfNewToken;
                    break;
                
                case NumberPhase.PastE:
                    if (ch == '+' || ch == '-')
                        phase = NumberPhase.PastExpSign;
                    else if (char.IsDigit(ch))
                        phase = NumberPhase.PastFirstExpDigit;
                    else
                        handling = CharHandling.InvalidatesToken;
                    break;
                
                case NumberPhase.PastExpSign:
                    if (char.IsDigit(ch))
                        phase = NumberPhase.PastFirstExpDigit;
                    else
                        handling = CharHandling.InvalidatesToken;
                    break;
                
                case NumberPhase.PastFirstExpDigit:
                    if (!char.IsDigit(ch))
                        handling = CharHandling.StartOfNewToken;
                    break;
            }
            
            if (handling==CharHandling.InvalidatesToken)
                state.Throw("Bad character while processing number");

            if (handling == CharHandling.StartOfNewToken)
            {
                lookedAhead = true;
                return state.MakeTokenFromBuffer(TokenType.Number);
            }
            
            state.Buffer.Append(enumerator.Current);
            state.Advance(false);

            if (enumerator.MoveNext())
                continue;
            
            lookedAhead = false;
            switch (phase)
            {
                case NumberPhase.PastFirstWhole:
                case NumberPhase.PastWhole:
                case NumberPhase.PastFirstFractional:
                case NumberPhase.PastFirstExpDigit:
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
        PastFirstWhole,
        PastWhole,
        PastDecimalPoint,
        PastFirstFractional,
        PastE,
        PastExpSign,
        PastFirstExpDigit,
    }

    private enum CharHandling
    {
        InvalidatesToken,
        ValidAndConsumed,
        StartOfNewToken,
    }
}
