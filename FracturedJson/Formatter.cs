using System;
using System.Collections.Generic;
using System.Linq;
using FracturedJson.Formatting;
using FracturedJson.Parsing;

namespace FracturedJson;

/// <summary>
/// Class that writes JSON data in a human-friendly format.  Comments are optionally supported.  While many options
/// are supported through <see cref="FracturedJsonOptions"/>, generally this class should "just work", producing
/// reasonable output for any JSON doc.
/// </summary>
public class Formatter
{
    public FracturedJsonOptions Options { get; set; } = new();
    public Func<string,int> StringLengthFunc { get; set; } = StringLengthByCharCount;

    public string Reformat(IEnumerable<char> jsonText, int startingDepth)
    {
        _buffer.Clear();
        _pads = new PaddedFormattingTokens(Options, StringLengthFunc);
        var parser = new Parser() { Options = Options };
        var docModel = parser.ParseTopLevel(jsonText, false);
        foreach(var item in docModel)
        {
            ComputeItemLengths(item);
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

    /// <summary>
    /// Runs StringLengthFunc on every part of every item and stores the value.  Also computes the total minimum
    /// length, which for arrays and objects includes their child lengths.  We're going to use these values a lot,
    /// and we don't want to run StringLengthFunc more than needed in case it's expensive.
    /// </summary>
    private void ComputeItemLengths(JsonItem item)
    {
        const char newline = '\n';
        foreach(var child in item.Children)
            ComputeItemLengths(child);

        item.NameLength = StringLengthFunc(item.Name);
        item.ValueLength = StringLengthFunc(item.Value);
        item.PrefixCommentLength = StringLengthFunc(item.PrefixComment);
        item.MiddleCommentLength = StringLengthFunc(item.MiddleComment);
        item.PostfixCommentLength = StringLengthFunc(item.PostfixComment);
        item.RequiresMultipleLines =
            (item.Type is JsonItemType.BlankLine or JsonItemType.BlockComment or JsonItemType.LineComment) 
            || item.Children.Any(ch => ch.RequiresMultipleLines || ch.IsPostCommentLineStyle)
            || item.PrefixComment.Contains(newline)
            || item.MiddleComment.Contains(newline) 
            || item.PostfixComment.Contains(newline) 
            || item.Value.Contains(newline);

        if (item.Type is JsonItemType.Array or JsonItemType.Object)
        {
            var padType = GetPaddingType(item);
            item.ValueLength = 
                _pads.StartLen(item.Type, padType)
                + _pads.EndLen(item.Type, padType)
                + item.Children.Sum(ch => ch.MinimumTotalLength)
                + Math.Max(0, _pads.CommaLen * (item.Children.Count - 1));
        }

        // Note that we're not considering this item's own trailing comma, if any.  But we are considering
        // commas between children.
        item.MinimumTotalLength =
            ((item.PrefixCommentLength > 0) ? item.PrefixCommentLength + _pads.CommentLen : 0)
            + ((item.NameLength > 0) ? item.NameLength + _pads.ColonLen : 0)
            + ((item.MiddleCommentLength > 0) ? item.MiddleCommentLength + _pads.CommentLen : 0)
            + item.ValueLength
            + ((item.PostfixCommentLength > 0) ? item.PostfixCommentLength + _pads.CommentLen : 0);
    }

    /// <summary>
    /// Adds a formatted version of any item to the buffer, including indentation and newlines as needed.  This
    /// could span multiple lines.
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
    /// Tries to add the representation for an array or object to the buffer, including all necessary indents, newlines, 
    /// etc., if the array/object qualifies.
    /// </summary>
    /// <returns>True if the content was added.</returns>
    private bool FormatContainerInline(JsonItem item, int depth, bool includeTrailingComma)
    {
        if (item.RequiresMultipleLines)
            return false;
        var lengthToConsider = item.MinimumTotalLength + ((includeTrailingComma) ? _pads.CommaLen : 0);
        if (item.Complexity > Options.MaxInlineComplexity  || lengthToConsider > AvailableLineSpace(depth))
            return false;

        _buffer.Add(Options.PrefixString, _pads.Indent(depth));
        InlineElement(_buffer, item, includeTrailingComma);
        _buffer.Add(_pads.EOL);

        return true;
    }

    /// <summary>
    /// Tries to add the representation of this array to the buffer, including indents and things, spanning multiple 
    /// lines but with each child written inline.
    /// </summary>
    /// <returns>True if the content was added</returns>
    private bool FormatContainerCompactMultiline(JsonItem item, int depth, bool includeTrailingComma)
    {
        if (item.Type != JsonItemType.Array)
            return false;
        if (item.Complexity > Options.MaxCompactArrayComplexity)
            return false;
        if (item.RequiresMultipleLines)
            return false;

        // If all items are alike, we'll want to format each element as if it were a table row.
        var template = new TableTemplate(_pads, !Options.DontJustifyNumbers);
        template.MeasureTableRoot(item);
        var templateSize = template.ComputeSize();

        // If we can't fit lots of them on a line, compact multiline isn't a good choice.  Table would likely
        // be better.
        var likelyAvailableLineSpace = AvailableLineSpace(depth+1);
        var avgItemWidth = _pads.CommaLen
                           + ((template.IsRowDataCompatible)
                               ? templateSize
                               : item.Children.Sum(ch => ch.MinimumTotalLength) / item.Children.Count);
        if (avgItemWidth * Options.MinCompactArrayRowItems > likelyAvailableLineSpace)
            return false;

        var depthAfterColon = StandardFormatStart(item, depth);

        // Starting bracket (with no EOL).
        _buffer.Add(_pads.Start(item.Type, BracketPaddingType.Empty));

        var availableLineSpace = AvailableLineSpace(depthAfterColon+1);
        var remainingLineSpace = -1;
        for (var i=0; i<item.Children.Count; ++i)
        {
            // Figure out whether the next item fits on the current line.  If not, start a new one.
            var child = item.Children[i];
            var needsComma = (i < item.Children.Count - 1);
            var spaceNeededForNext = ((needsComma) ? _pads.CommaLen : 0) + 
                                     ((template.IsRowDataCompatible) ? templateSize : child.MinimumTotalLength);

            if (remainingLineSpace < spaceNeededForNext)
            {
                _buffer.Add(_pads.EOL, Options.PrefixString, _pads.Indent(depthAfterColon+1));
                remainingLineSpace = availableLineSpace;
            }
            
            // Write it out
            if (template.IsRowDataCompatible)
                InlineTableRowSegment(_buffer, template, child, needsComma, false);
            else
                InlineElement(_buffer, child, needsComma);
            remainingLineSpace -= spaceNeededForNext;
        }

        // The previous line won't have ended yet, so do a line feed and indent before the closing bracket.
        _buffer.Add(_pads.EOL, Options.PrefixString, _pads.Indent(depthAfterColon),
            _pads.End(item.Type, BracketPaddingType.Empty));

        StandardFormatEnd(item, includeTrailingComma);
        return true;
    }

    /// <summary>
    /// Tries to format this array/object as a table.  That is, each of this JsonItem's children are each written
    /// as a single line, with their pieces formatted to line up.  This only works if the structures and types
    /// are consistent for all rows.
    /// </summary>
    /// <returns>True if the content was added</returns>
    private bool FormatContainerTable(JsonItem item, int depth, bool includeTrailingComma)
    {
        // If this element's children are too complex to be written inline, don't bother.
        if (item.Complexity > Options.MaxTableRowComplexity + 1)
            return false;
        
        var availableSpace = AvailableLineSpace(depth + 1) - _pads.CommaLen;

        // Create a helper object to measure how much space we'll need.  If this item's children aren't sufficiently
        // similar, CanBeUsedInTable will be false.
        var template = new TableTemplate(_pads, !Options.DontJustifyNumbers);
        template.MeasureTableRoot(item);
        if (!template.IsRowDataCompatible)
            return false;

        if (!template.TryToFit(availableSpace))
            return false;

        var depthAfterColon = StandardFormatStart(item, depth);
        _buffer.Add(_pads.Start(item.Type, BracketPaddingType.Empty), _pads.EOL);

        for (var i=0; i<item.Children.Count; ++i)
        {
            var rowItem = item.Children[i];
            if (rowItem.Type is JsonItemType.BlankLine)
            {
                FormatBlankLine();
                continue;
            }
            if (rowItem.Type is JsonItemType.LineComment or JsonItemType.BlockComment)
            {
                FormatStandaloneComment(rowItem, depthAfterColon+1);
                continue;
            }
            
            _buffer.Add(Options.PrefixString, _pads.Indent(depthAfterColon+1));
            InlineTableRowSegment(_buffer, template, rowItem, (i<item.Children.Count-1), true);
            _buffer.Add(_pads.EOL);
        }
        
        _buffer.Add(Options.PrefixString, _pads.Indent(depthAfterColon), _pads.End(item.Type, BracketPaddingType.Empty));
        StandardFormatEnd(item, includeTrailingComma);

        return true;
    }
    

    /// <summary>
    /// Adds the representation for an array or object to the buffer, including all necessary indents, newlines, etc.,
    /// broken out on separate lines.  This is the most general case that always works.
    /// </summary>
    private void FormatContainerExpanded(JsonItem item, int depth, bool includeTrailingComma)
    {
        var depthAfterColon = StandardFormatStart(item, depth);
        _buffer.Add(_pads.Start(item.Type, BracketPaddingType.Empty), _pads.EOL);
        
        for (var i=0; i<item.Children.Count; ++i)
            FormatItem(item.Children[i], depthAfterColon+1, (i<item.Children.Count-1));

        _buffer.Add(Options.PrefixString, _pads.Indent(depthAfterColon), _pads.End(item.Type, BracketPaddingType.Empty));
        StandardFormatEnd(item, includeTrailingComma);
    }

    /// <summary>
    /// Do the stuff that's the same for the start of every formatted item, like indents and prefix comments.
    /// </summary>
    /// <returns>Depth number to be used for everything after this.  In some cases, we print a prop label
    /// on one line, and then the value on another, at a greater indentation level.</returns>
    private int StandardFormatStart(JsonItem item, int depth)
    {
        // Everything is straightforward until the colon
        _buffer.Add(Options.PrefixString, _pads.Indent(depth));
        if (item.PrefixCommentLength > 0)
            _buffer.Add(item.PrefixComment, _pads.Comment);
        
        if (item.NameLength > 0)
            _buffer.Add(item.Name, _pads.Colon);
        
        if (item.MiddleCommentLength == 0)
            return depth;

        // If there's a middle comment, we write it on the same line and move along.  Easy.
        if (!item.MiddleComment.Contains('\n'))
        {
            _buffer.Add(item.MiddleComment, _pads.Comment);
            return depth;
        }

        // If the middle comment requires multiple lines, start a new line and indent everything after this.
        var commentRows = NormalizeMultilineComment(item.MiddleComment, int.MaxValue);
        _buffer.Add(_pads.EOL);
        
        foreach (var row in commentRows)
            _buffer.Add(Options.PrefixString, _pads.Indent(depth+1), row, _pads.EOL);
        
        _buffer.Add(Options.PrefixString, _pads.Indent(depth+1));
        return depth + 1;
    }

    /// <summary>
    /// Do the stuff that's usually the same for the end of all formatted items, like trailing commas and postfix
    /// comments.
    /// </summary>
    private void StandardFormatEnd(JsonItem item, bool includeTrailingComma)
    {
        if (includeTrailingComma && item.IsPostCommentLineStyle)
            _buffer.Add(_pads.Comma);
        if (item.PostfixCommentLength > 0)
            _buffer.Add(_pads.Comment, item.PostfixComment);
        if (includeTrailingComma && !item.IsPostCommentLineStyle)
            _buffer.Add(_pads.Comma);
        _buffer.Add(_pads.EOL);
    }
    

    /// <summary>
    /// Adds the inline representation of this item to the buffer.  This includes all of this element's
    /// comments and children when appropriate.  It DOES NOT include indentation, newlines, or any of that.  This
    /// should only be called if item.RequiresMultipleLines is false.
    /// </summary>
    private void InlineElement(IBuffer buffer, JsonItem item, bool includeTrailingComma)
    {
        if (item.RequiresMultipleLines)
            throw new FracturedJsonException("Logic error - trying to inline invalid element");
        
        if (item.PrefixCommentLength > 0)
            buffer.Add(item.PrefixComment, _pads.Comment);
        
        if (item.NameLength > 0)
            buffer.Add(item.Name, _pads.Colon);

        if (item.MiddleCommentLength > 0)
            buffer.Add(item.MiddleComment, _pads.Comment);

        InlineElementRaw(buffer, item);

        if (includeTrailingComma && item.IsPostCommentLineStyle)
            buffer.Add(_pads.Comma);
        if (item.PostfixCommentLength > 0)
            buffer.Add(_pads.Comment, item.PostfixComment);
        if (includeTrailingComma && !item.IsPostCommentLineStyle)
            buffer.Add(_pads.Comma);
    }

    /// <summary>
    /// Adds just this element's value to be buffer, inlined.  (Possibly recursively.)  This does not include
    /// the item's comments (although it could include child elements' comments), or indentation.
    /// </summary>
    private void InlineElementRaw(IBuffer buffer, JsonItem item)
    {
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
        else
        {
            buffer.Add(item.Value);
        }
    }

    /// <summary>
    /// Adds this item's representation to the buffer inlined, formatted according to the given TableTemplate.
    /// </summary>
    private void InlineTableRowSegment(IBuffer buffer, TableTemplate template, JsonItem item, bool includeTrailingComma,
        bool isWholeRow)
    {
        if (template.PrefixCommentLength > 0)
            buffer.Add(item.PrefixComment, 
                _pads.Spaces(template.PrefixCommentLength - item.PrefixCommentLength), 
                _pads.Comment);
        
        if (template.NameLength > 0)
            buffer.Add(item.Name,
                _pads.Spaces(template.NameLength - item.NameLength),
                _pads.Colon);

        if (template.MiddleCommentLength > 0)
            buffer.Add(item.MiddleComment, 
                _pads.Spaces(template.MiddleCommentLength - item.MiddleCommentLength),
                _pads.Comment);

        if (template.Children.Count > 0 && item.Type != JsonItemType.Null)
        {
            if (template.Type is JsonItemType.Array)
                InlineTableRawArray(buffer, template, item);
            else
                InlineTableRawObject(buffer, template, item);
        }
        else if (template.IsFormattableNumber && item.Type != JsonItemType.Null)
        {
            buffer.Add(template.FormatNumber(item.Value));
        }
        else
        {
            InlineElementRaw(buffer, item);
            buffer.Add(_pads.Spaces(template.ValueLength - item.ValueLength));
        }

        // If there's a postfix line comment, the comma needs to happen first.  For block comments,
        // it would be better to put the comma after the comment.
        var commaGoesBeforeComment = item.IsPostCommentLineStyle || item.PostfixCommentLength == 0;
        if (commaGoesBeforeComment)
        {
            // For internal row segments, there won't be trailing comments for any of the rows.  But
            // if this item represents the entire row, then they'll all have commas except the last.
            // the isWholeRow param lets up put in padding to line that up right.
            if (includeTrailingComma)
                buffer.Add(_pads.Comma);
            else if (isWholeRow)
                buffer.Add(_pads.DummyComma);
        }
        
        if (template.PostfixCommentLength > 0)
            buffer.Add(_pads.Comment, 
                _pads.Spaces(template.PostfixCommentLength - item.PostfixCommentLength),
                item.PostfixComment);
        
        if (!commaGoesBeforeComment)
        {
            if (includeTrailingComma)
                buffer.Add(_pads.Comma);
            else if (isWholeRow)
                buffer.Add(_pads.DummyComma);
        }
    }

    private void InlineTableRawArray(IBuffer buffer, TableTemplate template, JsonItem item)
    {
        buffer.Add(_pads.ArrStart(template.PadType));
        for (var i = 0; i < template.Children.Count; ++i)
        {
            var isLastInTemplate = (i == template.Children.Count - 1);
            var isLastInArray = (i == item.Children.Count - 1);
            var isPastEndOfArray = (i >= item.Children.Count);
            var subTemplate = template.Children[i];

            if (isPastEndOfArray)
            {
                buffer.Add(_pads.Spaces(subTemplate.ComputeSize()));
                if (!isLastInTemplate)
                    buffer.Add(_pads.DummyComma);
            }
            else
            {
                InlineTableRowSegment(buffer, subTemplate, item.Children[i], !isLastInArray, false);
                if (isLastInArray && !isLastInTemplate)
                    buffer.Add(_pads.DummyComma);
            }
        }
        buffer.Add(_pads.ArrEnd(template.PadType));
    }

    private void InlineTableRawObject(IBuffer buffer, TableTemplate template, JsonItem item)
    {
        JsonItem? MatchingChild(TableTemplate temp) =>
            item.Children.FirstOrDefault(ch => ch.Name == temp.LocationInParent);

        var matches = template.Children.Select(sub => (sub, MatchingChild(sub)))
            .ToArray();
        var lastNonNullIdx = matches.Length - 1;
        while (lastNonNullIdx>=0 && matches[lastNonNullIdx].Item2 == null)
            lastNonNullIdx -= 1;

        buffer.Add(_pads.ObjStart(template.PadType));
        for (var i = 0; i < matches.Length; ++i)
        {
            var subTemplate = matches[i].sub;
            var subItem = matches[i].Item2;
            var isLastInObject = (i == lastNonNullIdx);
            var isLastInTemplate = (i == matches.Length - 1);
            if (subItem != null)
            {
                InlineTableRowSegment(buffer, subTemplate, subItem, !isLastInObject, false);
                if (isLastInObject && !isLastInTemplate)
                    buffer.Add(_pads.DummyComma);
            }
            else
            {
                buffer.Add(_pads.Spaces(subTemplate.ComputeSize()));
                if (!isLastInTemplate)
                    buffer.Add(_pads.DummyComma);
            }
        }
        buffer.Add(_pads.ObjEnd(template.PadType));
    }

    /// <summary>
    /// Adds a (possibly multiline) standalone comment to the buffer, with indents and newlines on each line.
    /// </summary>
    private void FormatStandaloneComment(JsonItem item, int depth)
    {
        var commentRows = NormalizeMultilineComment(item.Value, item.InputPosition.Column);
        
        foreach (var line in commentRows)
            _buffer.Add(Options.PrefixString, _pads.Indent(depth), line, _pads.EOL );
    }

    private void FormatBlankLine()
    {
        _buffer.Add(Options.PrefixString, _pads.EOL);
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

    /// <summary>
    /// Adds an item to the buffer, including comments and indents and such, where a comment between the
    /// prop name and prop value needs to span multiple lines.
    /// </summary>
    private void FormatSplitKeyValue(JsonItem item, int depth, bool includeTrailingComma)
    {
        StandardFormatStart(item, depth);
        _buffer.Add(item.Value);
        StandardFormatEnd(item, includeTrailingComma);
    }

    private BracketPaddingType GetPaddingType(JsonItem arrOrObj)
    {
        if (arrOrObj.Children.Count == 0)
            return BracketPaddingType.Empty;

        return (arrOrObj.Complexity >= 2) ? BracketPaddingType.Complex : BracketPaddingType.Simple;
    }

    /// <summary>
    /// Figures out how much room is allowed for inlining at this indentation level, considering
    /// <see cref="FracturedJsonOptions.MaxTotalLineLength"/> and <see cref="FracturedJsonOptions.MaxInlineLength"/>.
    /// </summary>
    private int AvailableLineSpace(int depth)
    {
        return Math.Min(Options.MaxInlineLength,
            Options.MaxTotalLineLength - _pads.PrefixStringLen - Options.IndentSpaces * depth);
    }

    private static string[] NormalizeMultilineComment(string comment, int firstLineColumn)
    {
        // Split the comment into separate lines, and get rid of that nasty \r\n stuff.  We'll write the
        // line endings that the user wants ourselves.
        var normalized = comment.Replace("\r", string.Empty);
        var commentRows = normalized.Split('\n')
            .Where(line => line.Length>0)
            .ToArray();
        
        /*
         * The first line doesn't include any leading whitespace, but subsequent lines probably do.
         * We want to remove leading whitespace from those rows, but only up to where the first line began.
         * The idea is to preserve spaces used to line up comments, like the ones before the asterisks
         * in THIS VERY COMMENT that you're reading RIGHT NOW.
         */
        for (var i = 1; i < commentRows.Length; ++i)
        {
            var line = commentRows[i];

            var nonWsIdx = 0;
            while (nonWsIdx < line.Length && nonWsIdx < firstLineColumn && char.IsWhiteSpace(line[nonWsIdx]))
                nonWsIdx += 1;

            commentRows[i] = line.Substring(nonWsIdx);
        }

        return commentRows;
    }
}
