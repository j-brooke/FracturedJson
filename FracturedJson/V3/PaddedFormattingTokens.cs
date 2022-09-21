using System;

namespace FracturedJson.V3;

public class PaddedFormattingTokens
{
    public string ArrEmpty { get; }
    public string ArrStSimple { get; }
    public string ArrEndSimple { get; }
    public string ArrStComp { get; }
    public string ArrEndComp { get; }
    public string ObjEmpty { get; }
    public string ObjStSimple { get; }
    public string ObjEndSimple { get; }
    public string ObjStComp { get; }
    public string ObjEndComp { get; }
    public string Comma { get; }
    public string Colon { get; }
    public string EOL { get; }

    public int ArrEmptyLen { get; }
    public int ArrStSimpleLen { get; }
    public int ArrEndSimpleLen { get; }
    public int ArrStCompLen { get; }
    public int ArrEndCompLen { get; }
    public int ObjEmptyLen { get; }
    public int ObjStSimpleLen { get; }
    public int ObjEndSimpleLen { get; }
    public int ObjStCompLen { get; }
    public int ObjEndCompLen { get; }
    public int CommaLen { get; }
    public int ColonLen { get; }
    public int PrefixStringLen { get; }
    
    public PaddedFormattingTokens(FracturedJsonOptions opts, Func<string,int> strLenFunc)
    {
        ArrEmpty = "[]";
        ArrStSimple = (opts.SimpleBracketPadding) ? "[ " : "[";
        ArrEndSimple = (opts.SimpleBracketPadding) ? " ]" : "]";
        ArrStComp = (opts.NestedBracketPadding) ? "[ " : "[";
        ArrEndComp = (opts.NestedBracketPadding) ? " ]" : "]";
        ObjEmpty = "{}";
        ObjStSimple = (opts.SimpleBracketPadding) ? "{ " : "{";
        ObjEndSimple = (opts.SimpleBracketPadding) ? " }" : "}";
        ObjStComp = (opts.NestedBracketPadding) ? "{ " : "{";
        ObjEndComp = (opts.NestedBracketPadding) ? " }" : "}";
        Comma = (opts.CommaPadding) ? ", " : ",";
        Colon = (opts.ColonPadding) ? ": " : ":";
        EOL = opts.JsonEolStyle switch
        {
            EolStyle.Crlf => "\r\n",
            EolStyle.Lf => "\n",
            _ => Environment.NewLine,
        };

        ArrEmptyLen = strLenFunc(ArrEmpty);
        ArrStSimpleLen = strLenFunc(ArrStSimple);
        ArrEndSimpleLen = strLenFunc(ArrEndSimple);
        ArrStCompLen = strLenFunc(ArrStComp);
        ArrEndCompLen = strLenFunc(ArrEndComp);
        ObjEmptyLen = strLenFunc(ObjEmpty);
        ObjStSimpleLen = strLenFunc(ObjStSimple);
        ObjEndSimpleLen = strLenFunc(ObjEndSimple);
        ObjStCompLen = strLenFunc(ObjStComp);
        ObjEndCompLen = strLenFunc(ObjEndComp);
        CommaLen = strLenFunc(Comma);
        ColonLen = strLenFunc(Colon);
        PrefixStringLen = strLenFunc(opts.PrefixString);
    }
}
