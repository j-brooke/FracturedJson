using System;
using System.Collections.Generic;
using System.Linq;

namespace FracturedJson.Formatting;

internal class PaddedFormattingTokens
{
    public string Comma { get; }
    public string Colon { get; }
    public string Comment { get; }
    public string EOL { get; }
    public int CommaLen { get; }
    public int ColonLen { get; }
    public int CommentLen { get; }
    public int PrefixStringLen { get; }
    public string DummyComma => Spaces(CommaLen);
    
    public PaddedFormattingTokens(FracturedJsonOptions opts, Func<string,int> strLenFunc)
    {
        _arrStart = new string[3];
        _arrStart[(int)BracketPaddingType.Empty] = "[";
        _arrStart[(int)BracketPaddingType.Simple] = (opts.SimpleBracketPadding) ? "[ " : "[";
        _arrStart[(int)BracketPaddingType.Complex] = (opts.NestedBracketPadding) ? "[ " : "[";

        _arrEnd = new string[3];
        _arrEnd[(int)BracketPaddingType.Empty] = "]";
        _arrEnd[(int)BracketPaddingType.Simple] = (opts.SimpleBracketPadding) ? " ]" : "]";
        _arrEnd[(int)BracketPaddingType.Complex] = (opts.NestedBracketPadding) ? " ]" : "]";
        
        _objStart = new string[3];
        _objStart[(int)BracketPaddingType.Empty] = "{";
        _objStart[(int)BracketPaddingType.Simple] = (opts.SimpleBracketPadding) ? "{ " : "{";
        _objStart[(int)BracketPaddingType.Complex] = (opts.NestedBracketPadding) ? "{ " : "{";

        _objEnd = new string[3];
        _objEnd[(int)BracketPaddingType.Empty] = "}";
        _objEnd[(int)BracketPaddingType.Simple] = (opts.SimpleBracketPadding) ? " }" : "}";
        _objEnd[(int)BracketPaddingType.Complex] = (opts.NestedBracketPadding) ? " }" : "}";
        
        Comma = (opts.CommaPadding) ? ", " : ",";
        Colon = (opts.ColonPadding) ? ": " : ":";
        Comment = (opts.CommentPadding) ? " " : string.Empty;
        EOL = opts.JsonEolStyle switch
        {
            EolStyle.Crlf => "\r\n",
            EolStyle.Lf => "\n",
            _ => Environment.NewLine,
        };

        _arrStartLen = _arrStart.Select(strLenFunc).ToArray();
        _arrEndLen = _arrEnd.Select(strLenFunc).ToArray();
        _objStartLen = _objStart.Select(strLenFunc).ToArray();
        _objEndLen = _objEnd.Select(strLenFunc).ToArray();

        // Create pre-made indent strings for levels 0 and 1 now.  We'll construct and cache others as needed.
        _indentStrings = new()
        {
            string.Empty,
            (opts.UseTabToIndent)? "\t" : new string(' ', opts.IndentSpaces)
        };

        CommaLen = strLenFunc(Comma);
        ColonLen = strLenFunc(Colon);
        CommentLen = strLenFunc(Comment);
        PrefixStringLen = strLenFunc(opts.PrefixString);
    }

    public string ArrStart(BracketPaddingType type)
    {
        return _arrStart[(int)type];
    }

    public string ArrEnd(BracketPaddingType type)
    {
        return _arrEnd[(int)type];
    }

    public string ObjStart(BracketPaddingType type)
    {
        return _objStart[(int)type];
    }

    public string ObjEnd(BracketPaddingType type)
    {
        return _objEnd[(int)type];
    }

    public string Start(JsonItemType elemType, BracketPaddingType bracketType)
    {
        return (elemType == JsonItemType.Array) ? ArrStart(bracketType) : ObjStart(bracketType);
    }
    
    public string End(JsonItemType elemType, BracketPaddingType bracketType)
    {
        return (elemType == JsonItemType.Array) ? ArrEnd(bracketType) : ObjEnd(bracketType);
    }
    
    public int ArrStartLen(BracketPaddingType type)
    {
        return _arrStartLen[(int)type];
    }

    public int ArrEndLen(BracketPaddingType type)
    {
        return _arrEndLen[(int)type];
    }

    public int ObjStartLen(BracketPaddingType type)
    {
        return _objStartLen[(int)type];
    }

    public int ObjEndLen(BracketPaddingType type)
    {
        return _objEndLen[(int)type];
    }

    public int StartLen(JsonItemType elemType, BracketPaddingType bracketType)
    {
        return (elemType == JsonItemType.Array) ? ArrStartLen(bracketType) : ObjStartLen(bracketType);
    }
    
    public int EndLen(JsonItemType elemType, BracketPaddingType bracketType)
    {
        return (elemType == JsonItemType.Array) ? ArrEndLen(bracketType) : ObjEndLen(bracketType);
    }

    public string Indent(int level)
    {
        // If we don't have a cached indent string for this level, create one from the smaller ones.
        if (level >= _indentStrings.Count)
        {
            for (var i = _indentStrings.Count; i <= level; ++i)
                _indentStrings.Add(_indentStrings[i-1] + _indentStrings[1]);
        }

        return _indentStrings[level];
    }

    public string Spaces(int quantity)
    {
        // todo - make smarter
        return new string(' ', quantity);
    }

    private readonly string[] _arrStart;
    private readonly string[] _arrEnd;
    private readonly string[] _objStart;
    private readonly string[] _objEnd;
    private readonly int[] _arrStartLen;
    private readonly int[] _arrEndLen;
    private readonly int[] _objStartLen;
    private readonly int[] _objEndLen;
    private readonly List<string> _indentStrings;
}
