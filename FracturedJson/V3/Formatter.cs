using System;
using System.Collections.Generic;
using System.Linq;
using FracturedJson.Tokenizer;

namespace FracturedJson.V3;

public class Formatter
{
    public FracturedJsonOptions Options { get; set; } = new();
    public Func<string,int> StringLengthFunc { get; set; } = StringLengthByCharCount;

    public string Reformat(IEnumerable<char> jsonText, int startingDepth)
    {
        var parser = new Parser() { Options = Options };
        var docModel = parser.ParseTopLevel(jsonText, false);
        foreach(var item in docModel)
        {
            Preprocess(item);
            FormatItem(item, startingDepth, false);
        }

        return _buffer.AsString();
    }

    public static int StringLengthByCharCount(string s)
    {
        return s.Length;
    }

    private readonly IBuffer _buffer = new StringBuilderBuffer();
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
            (item.Type is JsonItemType.BlankLine or JsonItemType.BlockComment or JsonItemType.LineComment) 
            || item.Children.Any(ch => ch.RequiresMultipleLines || ch.IsPostCommentLineStyle)
            || (item.PrefixComment != null && item.PrefixComment.Contains(newline)) 
            || (item.MiddleComment != null && item.MiddleComment.Contains(newline)) 
            || (item.PostfixComment != null && item.PostfixComment.Contains(newline)) 
            || (item.Value != null && item.Value.Contains(newline));

        var bracketLengths = 0;
        switch (item.Type)
        {
            case JsonItemType.Array:
            {
                var padType = GetPaddingType(item);
                bracketLengths = _pads.ArrStartLen(padType) + _pads.ArrEndLen(padType);
                break;
            }
            case JsonItemType.Object:
            {
                var padType = GetPaddingType(item);
                bracketLengths = _pads.ObjStartLen(padType) + _pads.ObjEndLen(padType);
                break;
            }
        }

        // Note that we're not considering this item's own trailing comma, if any.  But we are considering
        // commas between children.
        item.MinimumTotalLength =
            ((item.PrefixCommentLength > 0) ? item.PrefixCommentLength + _pads.CommentLen : 0)
            + ((item.NameLength > 0) ? item.NameLength + _pads.ColonLen : 0)
            + item.MiddleCommentLength
            + item.ValueLength
            + ((item.PostfixCommentLength > 0) ? item.PostfixCommentLength + _pads.CommentLen : 0)
            + bracketLengths
            + item.Children.Sum(ch => ch.MinimumTotalLength)
            + Math.Max(0, _pads.CommaLen * (item.Children.Count - 1));
    }

    private int NullSafeStringLength(string? s)
    {
        return (s == null) ? 0 : StringLengthFunc(s);
    }

    /// <summary>
    /// Adds a formatted version of any item to the buffer, including indentation and newlines as needed.
    /// </summary>
    private void FormatItem(JsonItem item, int depth, bool includeTrailingComma)
    {
        switch (item.Type)
        {
            case JsonItemType.Array:
            case JsonItemType.Object:
                FormatContainer(item, depth, includeTrailingComma);
                break;
            case JsonItemType.BlankLine:
                FormatBlankLine();
                break;
            case JsonItemType.BlockComment:
            case JsonItemType.LineComment:
                FormatStandaloneComment(item, depth);
                break;
            default:
                if (item.RequiresMultipleLines)
                    FormatSplitKeyValue(item, depth, includeTrailingComma);
                else
                    FormatInlineElement(item, depth, includeTrailingComma);
                break;
        }
    }

    /// <summary>
    /// Adds the representation for an array or object to the buffer, including all necessary indents, newlines, etc.
    /// The array/object might be formatted inline, compact multiline, table, or expanded, according to circumstances.
    /// </summary>
    private void FormatContainer(JsonItem item, int depth, bool includeTrailingComma)
    {
        if (FormatContainerInline(item, depth, includeTrailingComma))
            return;
        if (FormatContainerCompactMultiline(item, depth, includeTrailingComma))
            return;
        if (FormatContainerTable(item, depth, includeTrailingComma))
            return;
        FormatContainerExpanded(item, depth, includeTrailingComma);
    }

    /// <summary>
    /// Adds the representation for an array or object to the buffer, including all necessary indents, newlines, etc.,
    /// if the array/object qualifies.
    /// </summary>
    /// <returns>True if the content was added.</returns>
    private bool FormatContainerInline(JsonItem item, int depth, bool includeTrailingComma)
    {
        if (item.RequiresMultipleLines)
            return false;
        var maxInlineLength = Math.Min(Options.MaxInlineLength,
            Options.MaxTotalLineLength - _pads.PrefixStringLen - Options.IndentSpaces * depth);
        var lengthToConsider = item.MinimumTotalLength + ((includeTrailingComma) ? _pads.CommaLen : 0);
        if (lengthToConsider > maxInlineLength || item.Complexity > Options.MaxInlineComplexity)
            return false;

        _buffer.Add(Options.PrefixString, _pads.Indent(depth));
        InlineElement(_buffer, item, includeTrailingComma);
        _buffer.Add(_pads.EOL);

        return true;
    }

    private bool FormatContainerCompactMultiline(JsonItem item, int depth, bool includeTrailingComma)
    {
        // TODO: Implement
        return false;
    }

    private bool FormatContainerTable(JsonItem item, int depth, bool includeTrailingComma)
    {
        // TODO: Implement
        return false;
    }
    

    /// <summary>
    /// Adds the representation for an array or object to the buffer, including all necessary indents, newlines, etc.,
    /// broken out on separate lines.
    /// </summary>
    private void FormatContainerExpanded(JsonItem item, int depth, bool includeTrailingComma)
    {
        _buffer.Add(Options.PrefixString, _pads.Indent(depth));
        if (item.PrefixComment != null)
            _buffer.Add(item.PrefixComment, _pads.Comment);
        
        if (item.Name != null)
            _buffer.Add(item.Name, _pads.Colon);

        if (item.MiddleComment != null)
            _buffer.Add(item.MiddleComment);

        _buffer.Add(_pads.Start(item.Type, BracketPaddingType.Empty), _pads.EOL);
        
        for (var i=0; i<item.Children.Count; ++i)
            FormatItem(item.Children[i], depth+1, (i<item.Children.Count-1));

        _buffer.Add(Options.PrefixString, _pads.Indent(depth), _pads.End(item.Type, BracketPaddingType.Empty));
        
        if (includeTrailingComma && item.IsPostCommentLineStyle)
            _buffer.Add(_pads.Comma);
        if (item.PostfixComment != null)
            _buffer.Add(_pads.Comment, item.PostfixComment);
        if (includeTrailingComma && !item.IsPostCommentLineStyle)
            _buffer.Add(_pads.Comma);
        _buffer.Add(_pads.EOL);
    }

    /// <summary>
    /// Adds the inline representation of this item to the buffer.  This includes all of this element's
    /// comments and children when appropriate.  It doesn't include indentation, newlines, or any of that.  This
    /// should only be called if item.RequiresMultipleLines is false.
    /// </summary>
    private void InlineElement(IBuffer buffer, JsonItem item, bool includeTrailingComma)
    {
        if (item.RequiresMultipleLines)
            throw new FracturedJsonException("Logic error - trying to inline invalid element");
        
        if (item.PrefixComment != null)
            buffer.Add(item.PrefixComment, _pads.Comment);
        
        if (item.Name != null)
            buffer.Add(item.Name, _pads.Colon);

        if (item.MiddleComment != null)
            buffer.Add(item.MiddleComment);

        if (item.Type == JsonItemType.Array)
        {
            var padType = GetPaddingType(item);
            buffer.Add(_pads.ArrStart(padType));
            
            for (var i=0; i<item.Children.Count; ++i)
                InlineElement(buffer, item.Children[i], (i<item.Children.Count-1));
            
            buffer.Add(_pads.ArrEnd(padType));
        }
        else if (item.Type == JsonItemType.Object)
        {
            var padType = GetPaddingType(item);
            buffer.Add(_pads.ObjStart(padType));
            
            for (var i=0; i<item.Children.Count; ++i)
                InlineElement(buffer, item.Children[i], (i<item.Children.Count-1));
            
            buffer.Add(_pads.ObjEnd(padType));
        }
        else if (item.Value != null)
            buffer.Add(item.Value);

        if (includeTrailingComma && item.IsPostCommentLineStyle)
            buffer.Add(_pads.Comma);
        if (item.PostfixComment != null)
            buffer.Add(_pads.Comment, item.PostfixComment);
        if (includeTrailingComma && !item.IsPostCommentLineStyle)
            buffer.Add(_pads.Comma);
    }

    /// <summary>
    /// Adds a (possibly multiline) standalone comment to the buffer, with indents and newlines on each line.
    /// </summary>
    private void FormatStandaloneComment(JsonItem item, int depth)
    {
        if (item.Value == null)
            return;

        var commentRows = NormalizeMultilineComment(item.Value);
        
        foreach (var line in commentRows)
            _buffer.Add(Options.PrefixString, _pads.Indent(depth), line, _pads.EOL );
    }

    private void FormatBlankLine()
    {
        _buffer.Add(_pads.EOL);
    }

    /// <summary>
    /// Adds an element to the buffer that can be written as a single line, including indents and newlines.
    /// </summary>
    private void FormatInlineElement(JsonItem item, int depth, bool includeTrailingComma)
    {
        _buffer.Add(Options.PrefixString, _pads.Indent(depth));
        InlineElement(_buffer, item, includeTrailingComma);
        _buffer.Add(_pads.EOL);
    }

    private void FormatSplitKeyValue(JsonItem item, int depth, bool includeTrailingComma)
    {
        // TODO: Figure this out
        throw new NotImplementedException();
    }

    private BracketPaddingType GetPaddingType(JsonItem arrOrObj)
    {
        if (arrOrObj.Children.Count == 0)
            return BracketPaddingType.Empty;

        return (arrOrObj.Complexity >= 2) ? BracketPaddingType.Complex : BracketPaddingType.Simple;
    }

    private string[] NormalizeMultilineComment(string comment)
    {
        var spaces = new string(' ', Options.IndentSpaces);
        var normalized = comment.Replace("\t", spaces);
        var rows = normalized.Split('\n');

        for (var i = 1; i < rows.Length; ++i)
        {
            // TODO: replace this with smarter heuristic to line it up.
            rows[i] = rows[i].Trim();
        }

        return rows;
    }
}
