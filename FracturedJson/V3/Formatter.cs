using System;
using System.Collections.Generic;
using System.Linq;

namespace FracturedJson.V3;

public class Formatter
{
    public FracturedJsonOptions Options { get; set; } = new();
    public Func<string,int> StringLengthFunc { get; set; } = StringLengthByCharCount;

    public string Reformat(IEnumerable<char> jsonText, int startingDepth)
    {
        throw new NotImplementedException();
    }

    public static int StringLengthByCharCount(string s)
    {
        return s.Length;
    }

    private PaddedFormattingTokens _pads = new (new FracturedJsonOptions(), StringLengthByCharCount);
    
    private void Preprocess(JsonItem root)
    {
        _pads = new PaddedFormattingTokens(Options, StringLengthFunc);
        ComputeItemLengths(root);
    }

    /// <summary>
    /// Runs StringLengthFunc on every part of every item and stores the value.  Also computes the total minimum
    /// length, which for arrays and objects includes their child lengths.  We're going to use these values a lot,
    /// and we don't know if StringLengthFunc is costly.
    /// </summary>
    private void ComputeItemLengths(JsonItem item)
    {
        const char newline = '\n';
        foreach(var child in item.Children)
            ComputeItemLengths(child);

        item.NameLength = NullSafeStringLength(item.Name);
        item.ValueLength = NullSafeStringLength(item.Value);
        item.PrefixCommentLength = NullSafeStringLength(item.PrefixComment);
        item.MiddleCommentLength = NullSafeStringLength(item.MiddleComment);
        item.PostfixCommentLength = NullSafeStringLength(item.PostfixComment);
        item.RequiresMultipleLines =
            item.Children.Any(ch => ch.RequiresMultipleLines || ch.IsPostCommentLineStyle)
            || (item.PrefixComment != null && item.PrefixComment.Contains(newline))
            || (item.MiddleComment != null && item.MiddleComment.Contains(newline))
            || (item.PostfixComment != null && item.PostfixComment.Contains(newline))
            || (item.Value != null && item.Value.Contains(newline));

        var bracketLengths = 0;
        if (item.Type == JsonItemType.Array)
        {
            bracketLengths = item.Complexity switch
            {
                >= 2 => _pads.ArrStCompLen + _pads.ArrEndCompLen,
                1 => _pads.ArrStSimpleLen + _pads.ArrEndSimpleLen,
                _ => _pads.ArrEmptyLen
            };
        }
        else if (item.Type == JsonItemType.Object)
        {
            bracketLengths = item.Complexity switch
            {
                >= 2 => _pads.ObjStCompLen + _pads.ObjEndCompLen,
                1 => _pads.ObjStSimpleLen + _pads.ObjEndSimpleLen,
                _ => _pads.ObjEmptyLen
            };
        }

        // Note that we're always assuming there will be a comma after any given
        // element.  It's not strictly true, but it makes things so much simpler.
        item.MinimumTotalLength =
            item.PrefixCommentLength
            + item.NameLength
            + ((item.NameLength > 0) ? _pads.ColonLen : 0)
            + item.MiddleCommentLength
            + item.ValueLength
            + _pads.CommaLen
            + bracketLengths
            + item.Children.Sum(ch => ch.MinimumTotalLength);
    }

    private int NullSafeStringLength(string? s)
    {
        return (s == null) ? 0 : StringLengthFunc(s);
    }

    private void FormatItem(IBuffer buffer, JsonItem item, bool includeTrailingComma)
    {
        switch (item.Type)
        {
            case JsonItemType.Array:
                FormatArray(buffer, item, includeTrailingComma);
                break;
            case JsonItemType.Object:
                FormatObject(buffer, item, includeTrailingComma);
                break;
            case JsonItemType.BlankLine:
            case JsonItemType.BlockComment:
            case JsonItemType.LineComment:
                FormatStandaloneComment(buffer, item);
                break;
            default:
                FormatSimpleOrInlineElement(buffer, item, includeTrailingComma);
                break;
        }
    }

    private void FormatArray(IBuffer buffer, JsonItem item, bool includeTrailingComma)
    {
        // TODO: Try inline, try compact multiline, try table, try default
    }

    private void FormatObject(IBuffer buffer, JsonItem item, bool includeTrailingComma)
    {
        // TODO: Try inline, try table, try default
    }

    private void FormatSimpleOrInlineElement(IBuffer buffer, JsonItem item, bool includeTrailingComma)
    {
        if (item.PrefixComment != null)
            buffer.Add(item.PrefixComment);
        
        if (item.Name != null)
            buffer.Add(item.Name, _pads.Colon);

        if (item.MiddleComment != null)
            buffer.Add(item.MiddleComment);  // TODO - add logic to fix multiline comments

        if (item.Type == JsonItemType.Array || item.Type == JsonItemType.Object)
            FormatContainerValue(buffer, item);
        else if (item.Value != null)
            buffer.Add(item.Value);

        if (includeTrailingComma && item.IsPostCommentLineStyle)
            buffer.Add(_pads.Comma);
        if (item.PostfixComment != null)
            buffer.Add(item.PostfixComment);
        if (includeTrailingComma && item.IsPostCommentLineStyle)
            buffer.Add(_pads.Comma);
    }

    private void FormatContainerValue(IBuffer buffer, JsonItem item)
    {
        if (item.Children.Count == 0)
        {
            buffer.Add((item.Type == JsonItemType.Array) ? _pads.ArrEmpty : _pads.ObjEmpty);
            return;
        }

        // Figure out which variety of padded brackets to use.  There's a separate padding option for complex
        // containers than simple ones.
        string startBracket;
        string endBracket;

        if (item.Complexity >= 2)
        {
            if (item.Type == JsonItemType.Array)
                (startBracket, endBracket) = (_pads.ArrStComp, _pads.ArrEndComp);
            else
                (startBracket, endBracket) = (_pads.ObjStComp, _pads.ObjEndComp);
        }
        else
        {
            if (item.Type == JsonItemType.Array)
                (startBracket, endBracket) = (_pads.ArrStSimple, _pads.ArrEndSimple);
            else
                (startBracket, endBracket) = (_pads.ObjStSimple, _pads.ObjEndSimple);
        }
        
        buffer.Add(startBracket);
        
        for (var i = 0; i<item.Children.Count; ++i)
            FormatSimpleOrInlineElement(buffer, item.Children[i], (i < item.Children.Count - 1));
        
        buffer.Add(endBracket);
    }

    private void FormatStandaloneComment(IBuffer buffer, JsonItem item)
    {
        if (item.Value == null)
            return;
        if (!item.RequiresMultipleLines)
        {
            buffer.Add(item.Value);
            return;
        }

        var spaces = new string(' ', Options.IndentSpaces);
        var spaceNormalized = item.Value.Replace("\t", spaces);
        var rows = spaceNormalized.Split('\n');
        // TODO: To be continued
    }
}
