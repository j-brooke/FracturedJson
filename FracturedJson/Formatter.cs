using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    /// <summary>
    /// Settings controlling output and defining permissible input.
    /// </summary>
    public FracturedJsonOptions Options { get; set; } = new();

    /// <summary>
    /// Function that returns the width of the input string, for purposes of aligning text.
    /// </summary>
    public Func<string,int> StringLengthFunc { get; set; } = StringLengthByCharCount;

    /// <summary>
    /// Reads in JSON text (or JSON-with-comments), and returns a nicely-formatted string of the same content.
    /// </summary>
    public string Reformat(IEnumerable<char> jsonText, int startingDepth = 0)
    {
        var buffer = new StringBuilderBuffer();
        var parser = new Parser() { Options = Options };
        var docModel = parser.ParseTopLevel(jsonText, true);
        FormatTopLevel(docModel, startingDepth, buffer);

        buffer.Flush();
        return buffer.AsString();
    }

    /// <summary>
    /// Reads in JSON text (or JSON-with-comments), and writes a nicely-formatted version of the same content
    /// to the writer.
    /// </summary>
    public void Reformat(IEnumerable<char> jsonText, int startingDepth, TextWriter writer)
    {
        var buffer = new LineWriterBuffer(writer);
        var parser = new Parser() { Options = Options };
        var docModel = parser.ParseTopLevel(jsonText, true);
        FormatTopLevel(docModel, startingDepth, buffer);

        buffer.Flush();
    }

    /// <summary>
    /// Writes the serialized object as a nicely-formatted string.
    /// </summary>
    public string Serialize<T>(T obj, int startingDepth = 0, JsonSerializerOptions? serOpts = null)
    {
        var buffer = new StringBuilderBuffer();
        var rootElem = DomConverter.Convert(JsonSerializer.SerializeToElement(obj, serOpts), null);
        FormatTopLevel(new[] { rootElem }, startingDepth, buffer);

        buffer.Flush();
        return buffer.AsString();
    }

    /// <summary>
    /// Writes the serialized object to the writer as a nicely-formatted text.
    /// </summary>
    public void Serialize<T>(T obj, int startingDepth, TextWriter writer, JsonSerializerOptions? serOpts = null)
    {
        var buffer = new LineWriterBuffer(writer);
        var rootElem = DomConverter.Convert(JsonSerializer.SerializeToElement(obj, serOpts), null);
        FormatTopLevel(new[] { rootElem }, startingDepth, buffer);

        buffer.Flush();
    }

    /// <summary>
    /// Writes the given JSON input as a JSON string with all unnecessary space removed.  If comments and/or blank
    /// lines are allowed, they are written preserved.
    /// </summary>
    public string Minify(IEnumerable<char> jsonText)
    {
        var buffer = new StringBuilderBuffer();
        var parser = new Parser() { Options = Options };
        var docModel = parser.ParseTopLevel(jsonText, true);
        MinifyTopLevel(docModel, buffer);

        buffer.Flush();
        return buffer.AsString();
    }

    /// <summary>
    /// Writes the given JSON input to the writer as a JSON with all unnecessary space removed.  If comments and/or
    /// blank lines are allowed, they are written preserved.
    /// </summary>
    public void Minify(IEnumerable<char> jsonText, TextWriter writer)
    {
        var buffer = new LineWriterBuffer(writer);
        var parser = new Parser() { Options = Options };
        var docModel = parser.ParseTopLevel(jsonText, true);
        MinifyTopLevel(docModel, buffer);

        buffer.Flush();
    }

    /// <summary>
    /// Default method for determining the length of strings for alignment purposes.  This usually works fine for Latin
    /// characters with a monospaced font, but it doesn't handle East Asian characters appropriately.
    /// </summary>
    public static int StringLengthByCharCount(string s)
    {
        return s.Length;
    }

    private IBuffer _buffer = new NullBuffer();
    private PaddedFormattingTokens _pads = new (new FracturedJsonOptions(), StringLengthByCharCount);

    /// <summary>
    /// Display width of the line currently being written, in StringLengthFunc units.  Assigned at a few
    /// checkpoints (inline FormatItem, compact/table close, collapsed/un-collapsed close bracket) rather than
    /// on every buffer write.  Used to decide whether a closing bracket can share the last child's line.
    /// </summary>
    private int _currentLineLen;

    // ---- Entry
    // Pretty-print starts here: measure every item, then send each top-level item through FormatItem.

    private void FormatTopLevel(IEnumerable<JsonItem> docModel, int startingDepth, IBuffer buffer)
    {
        _buffer = buffer;
        _pads = new PaddedFormattingTokens(Options, StringLengthFunc);
        _currentLineLen = 0;

        foreach(var item in docModel)
        {
            ComputeItemLengths(item);
            StartLine(startingDepth);
            FormatItem(item, startingDepth, false, null);
            _buffer.EndLine(_pads.EOL);
        }

        _buffer = new NullBuffer();
    }

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

        item.ValueLength = item.Type switch
        {
            JsonItemType.Null => _pads.LiteralNullLen,
            JsonItemType.True => _pads.LiteralTrueLen,
            JsonItemType.False => _pads.LiteralFalseLen,
            _ => StringLengthFunc(item.Value)
        };

        item.NameLength = StringLengthFunc(item.Name);
        item.PrefixCommentLength = StringLengthFunc(item.PrefixComment);
        item.MiddleCommentLength = StringLengthFunc(item.MiddleComment);
        item.PostfixCommentLength = StringLengthFunc(item.PostfixComment);
        item.RequiresMultipleLines =
            !IsElement(item)
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

    // ---- Dispatcher
    // FormatItem writes one item.  FormatContainer picks a layout for an array/object.
    // The recursive thoroughfare is FormatItem -> FormatContainer -> FormatContainerExpanded -> FormatItem.
    // Inline, compact, and table do not send descendants back through FormatItem.

    /// <summary>
    /// Adds a formatted version of any item to the buffer, including internal newlines and indentation, but the
    /// indentation before the first line and line end after the last are the caller's responsibility.
    /// </summary>
    private void FormatItem(JsonItem item, int depth, bool includeTrailingComma, TableTemplate? parentTemplate)
    {
        switch (item.Type)
        {
            case JsonItemType.Array:
            case JsonItemType.Object:
                FormatContainer(item, depth, includeTrailingComma, parentTemplate);
                break;
            case JsonItemType.BlankLine:
                // Do nothing - the caller will treat this as a separate item and thus generate a new line.
                break;
            case JsonItemType.BlockComment:
            case JsonItemType.LineComment:
                FormatStandaloneComment(item, depth);
                break;
            default:
                if (item.RequiresMultipleLines)
                {
                    var depthAfterColon = StandardFormatStart(item, depth, parentTemplate);
                    _buffer.Add(item.Value);
                    StandardFormatEnd(item, includeTrailingComma);
                    SetLineWidthAfterPossiblySplitItem(item, depth, depthAfterColon, includeTrailingComma,
                        parentTemplate);
                }
                else
                {
                    InlineElement(item, includeTrailingComma, parentTemplate);
                    SetLineLengthAfterInlineItem(item, depth, includeTrailingComma, parentTemplate);
                }
                break;
        }
    }

    /// <summary>
    /// Adds the representation for an array or object to the buffer.  The array/object might be formatted inline,
    /// compact multiline, table, or expanded, according to circumstances.  The container's owner should have handled
    /// any necessary indenting before the start of this, and any newlines after.
    /// </summary>
    private void FormatContainer(JsonItem item, int depth, bool includeTrailingComma, TableTemplate? parentTemplate)
    {
        // Try to inline of compact-multiline format, as long as we're deeper than AlwaysExpandDepth.  Of course,
        // there may be other disqualifying factors that are discovered along the way.
        if (depth > Options.AlwaysExpandDepth)
        {
            if (FormatContainerInline(item, depth, includeTrailingComma, parentTemplate))
                return;
        }

        // Create a helper object to measure how much space we'll need.  If there's a chance that we'll be able to
        // format this container as a table or compact array, we need to measure recursively.  Otherwise, we still
        // might need top level measurements for aligning properties and such.
        var recursiveTemplate = item.Complexity <= Options.MaxCompactArrayComplexity ||
                                item.Complexity <= Options.MaxTableRowComplexity + 1;
        var template = new TableTemplate(_pads, Options.NumberListAlignment);
        template.MeasureTableRoot(item, recursiveTemplate);

        if (depth > Options.AlwaysExpandDepth)
        {
            if (FormatContainerCompactMultiline(item, depth, includeTrailingComma, template, parentTemplate))
                return;
        }

        // Allow table formatting at the specified depth, too.  So if this is a root level array and
        // AlwaysExpandDepth=0, we can table format it.  But if AlwaysExpandDepth=1, we can't format the root
        // as a table, since a table's children are always inlined (and thus not expanded).
        if (depth >= Options.AlwaysExpandDepth)
        {
            if (FormatContainerTable(item, depth, includeTrailingComma, template, parentTemplate))
                return;
        }

        FormatContainerExpanded(item, depth, includeTrailingComma, template, parentTemplate);
    }

    /// <summary>
    /// Writes a standalone comment.  Internal line breaks and indentation are taken care of here,
    /// but the indentation before the first line and line end after the last are the caller's
    /// responsibility.
    /// </summary>
    private void FormatStandaloneComment(JsonItem item, int depth)
    {
        var commentRows = NormalizeMultilineComment(item.Value, item.InputPosition.Column);
        if (commentRows.Length == 0)
            return;

        _buffer.Add(commentRows[0]);
        for (var i = 1; i < commentRows.Length; ++i)
        {
            _buffer.EndLine(_pads.EOL);
            StartLine(depth);
            _buffer.Add(commentRows[i]);
        }
    }

    // ---- Expanded
    // The recursive default: one child per line, each child goes back through FormatItem.

    /// <summary>
    /// Adds the representation for an array or object to the buffer, broken out on separate lines.  This is the most
    /// general case that always works.
    /// </summary>
    /// <param name="item">The container we need to write</param>
    /// <param name="depth">Indentation level</param>
    /// <param name="includeTrailingComma">True if this container should have a comma after it.</param>
    /// <param name="template">Measurements for *this* item and its children.  Used to line up the children's property
    /// values.</param>
    /// <param name="parentTemplate">Measurements for lining up this item's prop name/value with its siblings.</param>
    private void FormatContainerExpanded(JsonItem item, int depth, bool includeTrailingComma,
        TableTemplate template, TableTemplate? parentTemplate)
    {
        var depthAfterColon = StandardFormatStart(item, depth, parentTemplate);
        _buffer.Add(_pads.Start(item.Type, BracketPaddingType.Empty)).EndLine(_pads.EOL);

        // Decide whether to align this container's property values.  If so, pass this container's template along
        // to its children so they know how to align their property values.
        var alignProps = item.Type == JsonItemType.Object
                         && template.NameLength - template.NameMinimum <= Options.MaxPropNamePadding
                         && !template.AnyMiddleCommentHasNewline
                         && AvailableLineSpace(depth + 1) >= template.AtomicItemSize();
        var templateToPass = (alignProps) ? template : null;

        // Take note of the position of the last actual element, for comma decisions.  The last element
        // might not be the last item.
        var lastElementIndex = IndexOfLastElement(item.Children);
        for (var i=0; i<item.Children.Count; ++i)
        {
            StartLine(depthAfterColon+1);
            FormatItem(item.Children[i], depthAfterColon + 1, (i < lastElementIndex), templateToPass);

            if (i < item.Children.Count - 1)
                _buffer.EndLine(_pads.EOL);
        }

        WriteNonInlineCloseBracket(item, depthAfterColon, includeTrailingComma);
        StandardFormatEnd(item, includeTrailingComma);
    }

    // ---- Other container strategies
    // FormatContainer tries these (inline, compact, table) before falling back to expanded.
    // If we use any of these strategies, descendants don't go back to the main FormatItem recursion path - they
    // stay in the line writer or table-segment writer.

    /// <summary>
    /// Tries to add the representation for an array or object to the buffer.
    /// </summary>
    /// <returns>True if the content was added.</returns>
    private bool FormatContainerInline(JsonItem item, int depth, bool includeTrailingComma, TableTemplate? parentTemplate)
    {
        if (item.RequiresMultipleLines)
            return false;

        // If we need to line up this item's value with others from the parent container, use the parentTemplate's
        // measurements to account for padding.
        int prefixLength;
        int nameLength;
        if (parentTemplate != null)
        {
            prefixLength = (parentTemplate.PrefixCommentLength > 0)
                ? parentTemplate.PrefixCommentLength + _pads.CommentLen
                : 0;
            nameLength = (parentTemplate.NameLength > 0) ? parentTemplate.NameLength + _pads.ColonLen : 0;
        }
        else
        {
            prefixLength = (item.PrefixCommentLength > 0) ? item.PrefixCommentLength + _pads.CommentLen : 0;
            nameLength = (item.NameLength > 0) ? item.NameLength + _pads.ColonLen : 0;
        }

        var lengthToConsider = prefixLength
                               + nameLength
                               + ((item.MiddleCommentLength > 0) ? item.MiddleCommentLength + _pads.CommentLen : 0)
                               + item.ValueLength
                               + ((item.PostfixCommentLength > 0) ? item.PostfixCommentLength + _pads.CommentLen : 0)
                               + ((includeTrailingComma) ? _pads.CommaLen : 0);

        if (item.Complexity > Options.MaxInlineComplexity  || lengthToConsider > AvailableLineSpace(depth))
            return false;

        InlineElement(item, includeTrailingComma, parentTemplate);
        SetLineLengthAfterInlineItem(item, depth, includeTrailingComma, parentTemplate);

        return true;
    }

    /// <summary>
    /// Tries to add the representation of this array to the buffer, including indents and things, spanning multiple
    /// lines but with each child written inline and several of them per line.
    /// </summary>
    /// <returns>True if the content was added</returns>
    private bool FormatContainerCompactMultiline(JsonItem item, int depth, bool includeTrailingComma,
        TableTemplate template, TableTemplate? parentTemplate)
    {
        if (item.Type != JsonItemType.Array)
            return false;
        if (item.Children.Count == 0 || item.Children.Count < Options.MinCompactArrayRowItems)
             return false;
        if (item.Complexity > Options.MaxCompactArrayComplexity)
            return false;
        if (item.RequiresMultipleLines)
            return false;

        var useTableFormatting = template.Type is not (TableColumnType.Unknown or TableColumnType.Mixed);

        // If we can't fit lots of them on a line, compact multiline isn't a good choice.  Table would likely
        // be better.
        var likelyAvailableLineSpace = AvailableLineSpace(depth+1);
        var avgItemWidth = _pads.CommaLen
                           + ((useTableFormatting)
                               ? template.TotalLength
                               : item.Children.Sum(ch => ch.MinimumTotalLength) / item.Children.Count);
        if (avgItemWidth * Options.MinCompactArrayRowItems > likelyAvailableLineSpace)
            return false;

        var depthAfterColon = StandardFormatStart(item, depth, parentTemplate);
        _buffer.Add(_pads.Start(item.Type, BracketPaddingType.Empty));

        var availableLineSpace = AvailableLineSpace(depthAfterColon+1);
        var remainingLineSpace = -1;
        for (var i=0; i<item.Children.Count; ++i)
        {
            // Figure out whether the next item fits on the current line.  If not, start a new one.
            var child = item.Children[i];
            var needsComma = (i < item.Children.Count - 1);
            var spaceNeededForNext = ((needsComma) ? _pads.CommaLen : 0) +
                                     ((useTableFormatting) ? template.TotalLength : child.MinimumTotalLength);

            if (remainingLineSpace < spaceNeededForNext)
            {
                _buffer.EndLine(_pads.EOL);
                StartLine(depthAfterColon+1);
                remainingLineSpace = availableLineSpace;
            }

            // Write it out
            if (useTableFormatting)
                InlineTableRowSegment(template, child, needsComma, false);
            else
                InlineElement(child, needsComma, null);
            remainingLineSpace -= spaceNeededForNext;
        }

        _currentLineLen = Options.MaxTotalLineLength - remainingLineSpace;
        WriteNonInlineCloseBracket(item, depthAfterColon, includeTrailingComma);
        StandardFormatEnd(item, includeTrailingComma);
        return true;
    }

    /// <summary>
    /// Tries to format this array/object as a table.  That is, each of this JsonItem's children are each written
    /// as a single line, with their pieces formatted to line up.  This only works if the structures and types
    /// are consistent for all rows.
    /// </summary>
    /// <returns>True if the content was added</returns>
    private bool FormatContainerTable(JsonItem item, int depth, bool includeTrailingComma, TableTemplate template,
        TableTemplate? parentTemplate)
    {
        // If this element's children are too complex to be written inline, don't bother.
        if (item.Complexity > Options.MaxTableRowComplexity + 1)
            return false;

        // If any particular row would require multiple lines, we can't table format this as a table.
        if (template.RequiresMultipleLines)
            return false;

        // Figure out the space available to each row, not counting ending commas.  Note that if there's a middle
        // comment with a newline, we'll be indenting more than normal.
        var availableSpaceDepth = (item.MiddleCommentHasNewline) ? depth + 2 : depth + 1;
        var availableSpace = AvailableLineSpace(availableSpaceDepth) - _pads.CommaLen;

        // If any child element is too long even without formatting, don't bother.
        var isChildTooLong = item.Children
            .Where(IsElement)
            .Any(ch => ch.MinimumTotalLength > availableSpace);
        if (isChildTooLong)
            return false;

        // If the rows don't fit with everything (including descendants) tabular, try dropping the columns for
        // the deepest nested items, repeatedly, until it either fits or we give up.
        //
        // For instance, here's an example of what fully tabular would look like:
        // [
        //     { "a":   3, "b": { "x": 19, "y":  -4           } },
        //     { "a": 147, "b": {          "y": 111, "z": -99 } }
        // ]
        // If that doesn't work, we try this:
        // [
        //     { "a":   3, "b": { "x": 19, "y": -4 }   },
        //     { "a": 147, "b": { "y": 111, "z": -99 } }
        // ]
        if (!template.TryToFit(availableSpace) || template.Type == TableColumnType.Mixed)
            return false;

        var depthAfterColon = StandardFormatStart(item, depth, parentTemplate);
        _buffer.Add(_pads.Start(item.Type, BracketPaddingType.Empty)).EndLine(_pads.EOL);

        // Take note of the position of the last actual element, for comma decisions.  The last element
        // might not be the last item.
        var lastElementIndex = IndexOfLastElement(item.Children);
        for (var i=0; i<item.Children.Count; ++i)
        {
            StartLine(depthAfterColon+1);
            var rowItem = item.Children[i];
            if (rowItem.Type is JsonItemType.BlankLine)
            {
                // Do nothing - we will write an EOL at the end of the loop.
            }
            else if (rowItem.Type is JsonItemType.LineComment or JsonItemType.BlockComment)
            {
                FormatStandaloneComment(rowItem, depthAfterColon+1);
            }
            else
            {
                InlineTableRowSegment(template, rowItem, (i<lastElementIndex), true);
            }

            if (i < item.Children.Count - 1)
                _buffer.EndLine(_pads.EOL);
        }

        _currentLineLen = LinePrefixWidth(depthAfterColon + 1) + template.TotalLength + _pads.CommaLen;
        WriteNonInlineCloseBracket(item, depthAfterColon, includeTrailingComma);
        StandardFormatEnd(item, includeTrailingComma);

        return true;
    }

    // ---- Line writer
    // Write an item on the current line.  Used by FormatItem, inline containers, compact, and nested inlines.
    // Precondition: !RequiresMultipleLines (no standalone comments).

    /// <summary>
    /// Adds the inline representation of this item to the buffer.  This includes all of this element's
    /// comments and children when appropriate.  It DOES NOT include indentation, newlines, or any of that.  This
    /// should only be called if item.RequiresMultipleLines is false.
    /// </summary>
    private void InlineElement(JsonItem item, bool includeTrailingComma, TableTemplate? parentTemplate)
    {
        FracturedJsonException.ThrowIf(item.RequiresMultipleLines, "Logic error - trying to inline invalid element");

        WritePrefixNameMiddle(item, parentTemplate);
        InlineElementRaw(item);
        StandardFormatEnd(item, includeTrailingComma);
    }

    /// <summary>
    /// Adds just this element's value to be buffer, inlined.  (Possibly recursively.)  This does not include
    /// the item's comments (although it will include child elements' comments), or indentation.
    /// </summary>
    private void InlineElementRaw(JsonItem item)
    {
        if (item.Type == JsonItemType.Array)
        {
            var padType = GetPaddingType(item);
            _buffer.Add(_pads.ArrStart(padType));

            for (var i=0; i<item.Children.Count; ++i)
                InlineElement(item.Children[i], (i<item.Children.Count-1), null);

            _buffer.Add(_pads.ArrEnd(padType));
        }
        else if (item.Type == JsonItemType.Object)
        {
            var padType = GetPaddingType(item);
            _buffer.Add(_pads.ObjStart(padType));

            for (var i=0; i<item.Children.Count; ++i)
                InlineElement(item.Children[i], (i<item.Children.Count-1), null);

            _buffer.Add(_pads.ObjEnd(padType));
        }
        else
        {
            _buffer.Add(item.Value);
        }
    }

    // ---- Table segments
    // Column-aligned inline writing.  Used by whole tables and by compact arrays.
    // Nested arrays/objects stay in this neighborhood; they never call FormatContainer.

    /// <summary>
    /// Adds this item's representation to the buffer inlined, formatted according to the given TableTemplate.
    /// </summary>
    private void InlineTableRowSegment(TableTemplate template, JsonItem item, bool includeTrailingComma,
        bool isWholeRow)
    {
        WritePrefixNameMiddle(item, template);

        var commaPos = GetTableCommaPosition(template, item);

        // If we're asked to include a comma, do it.  For internal segments, if we don't supply a comma, the padding
        // will work out elsewhere.  But if this segment is the whole row of a table, we need to supply a dummy
        // comma due to possible end-of-line comment complications.
        var commaType = (includeTrailingComma)
            ? _pads.Comma
            : (isWholeRow)
                ? _pads.DummyComma
                : string.Empty;

        InlineTableValue(template, item, commaPos, commaType);

        if (commaPos == CommaPosition.AfterValuePadding)
            _buffer.Add(commaType);

        if (template.PostfixCommentLength > 0)
            _buffer.Add(_pads.Comment, item.PostfixComment);

        if (commaPos == CommaPosition.BeforeCommentPadding)
            _buffer.Add(commaType);

        _buffer.Spaces(template.PostfixCommentLength - item.PostfixCommentLength);

        if (commaPos == CommaPosition.AfterCommentPadding)
            _buffer.Add(commaType);
    }

    /// <summary>
    /// Where this row segment's comma goes relative to value padding and postfix-comment padding.
    /// Line-style postfix comments consume the rest of the line, so they use the value-padding slots.
    /// Block postfix comments occupy their own padded field, which adds the comment-padding slots.
    /// </summary>
    private CommaPosition GetTableCommaPosition(TableTemplate template, JsonItem item)
    {
        var commaBeforePad = Options.TableCommaPlacement == TableCommaPlacement.BeforePadding
                             || (Options.TableCommaPlacement == TableCommaPlacement.BeforePaddingExceptNumbers
                                 && template.Type is not TableColumnType.Number);

        var columnHasBlockPostfix = template.PostfixCommentLength > 0 && !template.IsAnyPostCommentLineStyle;
        if (!columnHasBlockPostfix)
            return commaBeforePad ? CommaPosition.BeforeValuePadding : CommaPosition.AfterValuePadding;

        if (item.PostfixCommentLength > 0)
            return commaBeforePad ? CommaPosition.BeforeCommentPadding : CommaPosition.AfterCommentPadding;

        // This segment has no postfix comment, but siblings do.  AfterPadding still wants the comma in the
        // comment field so it lines up; BeforePadding clings to the value.
        return commaBeforePad ? CommaPosition.BeforeValuePadding : CommaPosition.AfterCommentPadding;
    }

    /// <summary>
    /// Writes this row segment's value, plus a comma if it belongs immediately after the value (before value
    /// padding).  Number lists can embed that comma in their alignment padding, so it has to be supplied here
    /// rather than after we return.
    /// </summary>
    private void InlineTableValue(TableTemplate template, JsonItem item, CommaPosition commaPos, string commaType)
    {
        if (template.Children.Count > 0 && item.Type != JsonItemType.Null)
        {
            if (template.Type is TableColumnType.Array)
                InlineTableRawArray(template, item);
            else
                InlineTableRawObject(template, item);
            if (commaPos == CommaPosition.BeforeValuePadding)
                _buffer.Add(commaType);

            // Special adjustment if the object/array is shorter than the literal "null".
            if (template.ShorterThanNullAdjustment > 0)
                _buffer.Spaces(template.ShorterThanNullAdjustment);
        }
        else if (template.Type is TableColumnType.Number)
        {
            var numberCommaType = (commaPos == CommaPosition.BeforeValuePadding) ? commaType : string.Empty;
            template.FormatNumber(_buffer, item, numberCommaType);
        }
        else
        {
            InlineElementRaw(item);
            if (commaPos == CommaPosition.BeforeValuePadding)
                _buffer.Add(commaType);
            _buffer.Spaces(template.CompositeValueLength - item.ValueLength);
        }
    }

    /// <summary>
    /// Adds just this ARRAY's value inlined, not worrying about comments and prop names and stuff.
    /// </summary>
    private void InlineTableRawArray(TableTemplate template, JsonItem item)
    {
        _buffer.Add(_pads.ArrStart(template.PadType));
        for (var i = 0; i < template.Children.Count; ++i)
        {
            var isLastInTemplate = (i == template.Children.Count - 1);
            var isLastInArray = (i == item.Children.Count - 1);
            var subItem = (i < item.Children.Count) ? item.Children[i] : null;
            InlineOrPadTableRowSegment(template.Children[i], subItem, isLastInArray, isLastInTemplate);
        }
        _buffer.Add(_pads.ArrEnd(template.PadType));
    }

    /// <summary>
    /// Adds just this OBJECT's value inlined, not worrying about comments and prop names and stuff.
    /// </summary>
    private void InlineTableRawObject(TableTemplate template, JsonItem item)
    {
        JsonItem? MatchingChild(TableTemplate temp) =>
            item.Children.FirstOrDefault(ch => ch.Name == temp.LocationInParent);

        // For every property in the template, find the corresponding element in this object, if any.
        var matches = template.Children.Select(sub => (sub, MatchingChild(sub)))
            .ToArray();

        // We need to know the last item in the sequence that has a real value, in order to make the commas work out.
        var lastNonNullIdx = matches.Length - 1;
        while (lastNonNullIdx>=0 && matches[lastNonNullIdx].Item2 == null)
            lastNonNullIdx -= 1;

        _buffer.Add(_pads.ObjStart(template.PadType));
        for (var i = 0; i < matches.Length; ++i)
        {
            var subTemplate = matches[i].sub;
            var subItem = matches[i].Item2;
            var isLastInObject = (i == lastNonNullIdx);
            var isLastInTemplate = (i == matches.Length - 1);
            InlineOrPadTableRowSegment(subTemplate, subItem, isLastInObject, isLastInTemplate);
        }
        _buffer.Add(_pads.ObjEnd(template.PadType));
    }

    /// <summary>
    /// Writes a nested row segment, or enough spaces to stand in for a child the template expected but this
    /// container does not have.  Dummy commas keep later segments aligned when the container is shorter than
    /// the template (ragged arrays, omitted object keys).
    /// </summary>
    private void InlineOrPadTableRowSegment(TableTemplate subTemplate, JsonItem? subItem, bool isLastInContainer,
        bool isLastInTemplate)
    {
        if (subItem != null)
        {
            InlineTableRowSegment(subTemplate, subItem, !isLastInContainer, false);
            if (isLastInContainer && !isLastInTemplate)
                _buffer.Add(_pads.DummyComma);
        }
        else
        {
            _buffer.Spaces(subTemplate.TotalLength);
            if (!isLastInTemplate)
                _buffer.Add(_pads.DummyComma);
        }
    }

    private enum CommaPosition
    {
        BeforeValuePadding,
        AfterValuePadding,
        BeforeCommentPadding,
        AfterCommentPadding,
    }

    // ---- Item chrome
    // Prefix comment, name, middle comment, trailing comma, postfix comment.
    // Used by expanded, compact, table, inline, and split key/value items.

    /// <summary>
    /// Writes the prefix comment, property name, and optionally the middle comment.  If a template is provided,
    /// each piece is padded to the corresponding column width.  This does not include indentation or the value.
    /// </summary>
    private void WritePrefixNameMiddle(JsonItem item, TableTemplate? template, bool includeMiddleComment = true)
    {
        if (template != null)
        {
            AddToBufferFixed(item.PrefixComment, item.PrefixCommentLength, template.PrefixCommentLength,
                _pads.Comment, false);
            AddToBufferFixed(item.Name, item.NameLength, template.NameLength, _pads.Colon,
                Options.ColonBeforePropNamePadding);
            if (includeMiddleComment)
                AddToBufferFixed(item.MiddleComment, item.MiddleCommentLength, template.MiddleCommentLength,
                    _pads.Comment, false);
        }
        else
        {
            AddToBuffer(item.PrefixComment, item.PrefixCommentLength, _pads.Comment);
            AddToBuffer(item.Name, item.NameLength, _pads.Colon);
            if (includeMiddleComment)
                AddToBuffer(item.MiddleComment, item.MiddleCommentLength, _pads.Comment);
        }
    }

    /// <summary>
    /// Do the stuff that's the same for the start of every formatted item, like prefix comments, property
    /// labels, colons, etc.  This does not include the initial indentation.
    /// </summary>
    /// <returns>Depth number to be used for everything after this.  In some cases, we print a prop label
    /// on one line, and then the value on another, at a greater indentation level.</returns>
    private int StandardFormatStart(JsonItem item, int depth, TableTemplate? parentTemplate)
    {
        // Write the middle comment here only if it fits on this line.  A multiline middle comment is handled
        // below, because it changes indentation for everything that follows.
        WritePrefixNameMiddle(item, parentTemplate,
            includeMiddleComment: item.MiddleCommentLength > 0 && !item.MiddleCommentHasNewline);

        if (item.MiddleCommentLength == 0 || !item.MiddleCommentHasNewline)
            return depth;

        // If the middle comment requires multiple lines, start a new line and indent everything after this.
        var commentRows = NormalizeMultilineComment(item.MiddleComment, int.MaxValue);
        _buffer.EndLine(_pads.EOL);

        foreach (var row in commentRows)
        {
            StartLine(depth + 1);
            _buffer.Add(row).EndLine(_pads.EOL);
        }

        StartLine(depth+1);
        return depth + 1;
    }

    /// <summary>
    /// Do the stuff that's usually the same for the end of all formatted items, like trailing commas and postfix
    /// comments.  This does not include an EOL.
    /// </summary>
    private void StandardFormatEnd(JsonItem item, bool includeTrailingComma)
    {
        if (includeTrailingComma && item.IsPostCommentLineStyle)
            _buffer.Add(_pads.Comma);
        if (item.PostfixCommentLength > 0)
            _buffer.Add(_pads.Comment, item.PostfixComment);
        if (includeTrailingComma && !item.IsPostCommentLineStyle)
            _buffer.Add(_pads.Comma);
    }

    /// <summary>
    /// Adds a string and a separator to the buffer.  For example, a prefix comment and the prefix separator.
    /// Neither is added if valueWidth is zero.
    /// </summary>
    private void AddToBuffer(string value, int valueWidth, string separator)
    {
        if (valueWidth <= 0)
            return;
        _buffer.Add(value, separator);
    }

    /// <summary>
    /// Adds a string and separator to the buffer, as well as enough padding spaces to make it fit the requested width.
    /// Nothing is added if fieldWidth is zero.
    /// </summary>
    private void AddToBufferFixed(string value, int valueWidth, int fieldWidth, string separator,
        bool separatorBeforePadding)
    {
        if (fieldWidth <= 0)
            return;
        var padWidth = fieldWidth - valueWidth;
        if (separatorBeforePadding)
            _buffer.Add(value, separator).Spaces(padWidth);
        else
            _buffer.Add(value).Spaces(padWidth).Add(separator);
    }

    /// <summary>
    /// Checks whether it's okay to write a closing bracket on the same line.
    /// </summary>
    private bool CanCollapseContainerClose(JsonItem container, BracketPaddingType padType, bool includeTrailingComma)
    {
        if (!Options.CollapseClosingBrackets)
            return false;

        if (container.Children.Count == 0)
            return false;

        var lastItemInContainer = container.Children[container.Children.Count - 1];
        var lastItemDisqualifies = lastItemInContainer.PostfixCommentLength > 0 || !IsElement(lastItemInContainer);
        if (lastItemDisqualifies)
            return false;

        var lineLengthIfCollapsed = _currentLineLen
                                    + _pads.EndLen(container.Type, padType)
                                    + ((includeTrailingComma) ? _pads.CommaLen : 0);
        return lineLengthIfCollapsed <= Options.MaxTotalLineLength;
    }

    /// <summary>
    /// Write a closing bracket - either on the same line or a new one.
    /// </summary>
    private void WriteNonInlineCloseBracket(JsonItem item, int depth, bool includeTrailingComma)
    {
        var padTypeIfInline = GetPaddingType(item);
        if (!CanCollapseContainerClose(item, padTypeIfInline, includeTrailingComma))
        {
            _buffer.EndLine(_pads.EOL);
            StartLine(depth);
            _buffer.Add(_pads.End(item.Type, BracketPaddingType.Empty));
            _currentLineLen = LinePrefixWidth(depth) + _pads.EndLen(item.Type, BracketPaddingType.Empty);
            return;
        }

        _buffer.Add(_pads.End(item.Type, padTypeIfInline));
        _currentLineLen += _pads.EndLen(item.Type, padTypeIfInline);
    }

    // ---- Plumbing

    /// <summary>
    /// Add the prefix string and indent
    /// </summary>
    private void StartLine(int depth)
    {
        _buffer.Add(Options.PrefixString, _pads.Indent(depth));
    }

    /// <summary>
    /// Figures out how much room is allowed for inlining at this indentation level, considering
    /// <see cref="FracturedJsonOptions.MaxTotalLineLength"/>, indentation size, and prefix string length.
    /// </summary>
    private int AvailableLineSpace(int depth)
    {
        return Options.MaxTotalLineLength - LinePrefixWidth(depth);
    }

    /// <summary>
    /// Prefix string plus indentation, in the same units as <see cref="AvailableLineSpace"/>.  Matches the
    /// planner (IndentSpaces per level) rather than the actual indent characters, so tab indents still
    /// reserve IndentSpaces of budget.
    /// </summary>
    private int LinePrefixWidth(int depth)
    {
        return _pads.PrefixStringLen + Options.IndentSpaces * depth;
    }

    /// <summary>
    /// Display width of an inlined item as actually written by <see cref="InlineElement"/>, not including
    /// the line's prefix/indent.
    /// </summary>
    private int InlineElementLength(JsonItem item, bool includeTrailingComma, TableTemplate? parentTemplate)
    {
        return PrefixNameMiddleLength(item, parentTemplate)
               + item.ValueLength
               + TrailingCommaAndPostfixLength(item, includeTrailingComma);
    }

    /// <summary>
    /// Length of the item's prefix and middle comments and prop name.
    /// </summary>
    private int PrefixNameMiddleLength(JsonItem item, TableTemplate? template)
    {
        if (template != null)
        {
            var width = 0;
            if (template.PrefixCommentLength > 0)
                width += template.PrefixCommentLength + _pads.CommentLen;
            if (template.NameLength > 0)
                width += template.NameLength + _pads.ColonLen;
            if (template.MiddleCommentLength > 0)
                width += template.MiddleCommentLength + _pads.CommentLen;
            return width;
        }

        var itemWidth = 0;
        if (item.PrefixCommentLength > 0)
            itemWidth += item.PrefixCommentLength + _pads.CommentLen;
        if (item.NameLength > 0)
            itemWidth += item.NameLength + _pads.ColonLen;
        if (item.MiddleCommentLength > 0)
            itemWidth += item.MiddleCommentLength + _pads.CommentLen;
        return itemWidth;
    }

    private int TrailingCommaAndPostfixLength(JsonItem item, bool includeTrailingComma)
    {
        var width = 0;
        if (item.PostfixCommentLength > 0)
            width += _pads.CommentLen + item.PostfixCommentLength;
        if (includeTrailingComma)
            width += _pads.CommaLen;
        return width;
    }

    /// <summary>
    /// Takes note of the current line position for the inline case.
    /// </summary>
    private void SetLineLengthAfterInlineItem(JsonItem item, int depth, bool includeTrailingComma,
        TableTemplate? parentTemplate)
    {
        _currentLineLen = LinePrefixWidth(depth) + InlineElementLength(item, includeTrailingComma, parentTemplate);
    }

    /// <summary>
    /// After a primitive that may have pushed its value onto a new line (multiline middle comment),
    /// record the width of whatever line the value ended on.
    /// </summary>
    private void SetLineWidthAfterPossiblySplitItem(JsonItem item, int originalDepth, int depthAfterColon,
        bool includeTrailingComma, TableTemplate? parentTemplate)
    {
        if (depthAfterColon == originalDepth)
        {
            SetLineLengthAfterInlineItem(item, originalDepth, includeTrailingComma, parentTemplate);
            return;
        }

        _currentLineLen = LinePrefixWidth(depthAfterColon) + item.ValueLength
                     + TrailingCommaAndPostfixLength(item, includeTrailingComma);
    }

    /// <summary>
    /// Determines whether the given array/object is simple or complex for padding purposes.
    /// </summary>
    private BracketPaddingType GetPaddingType(JsonItem arrOrObj)
    {
        if (arrOrObj.Children.Count == 0)
            return BracketPaddingType.Empty;

        return (arrOrObj.Complexity >= 2) ? BracketPaddingType.Complex : BracketPaddingType.Simple;
    }

    /// <summary>
    /// Returns a multiline comment string as an array of strings where newlines have been removed and leading space
    /// on each line has been trimmed as smartly as possible.
    /// </summary>
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

    /// <summary>
    /// True if the item is a real JSON value (as opposed to a standalone comment or blank line).
    /// </summary>
    private static bool IsElement(JsonItem item)
    {
        return item.Type is not
            (JsonItemType.BlankLine or JsonItemType.BlockComment or JsonItemType.LineComment);
    }

    /// <summary>
    /// Returns the index in the given list of the last element - that is, item that's not a standalone comment
    /// or blank line.  Used to decide where commas should go.
    /// </summary>
    private static int IndexOfLastElement(IList<JsonItem> itemList)
    {
        for (var i = itemList.Count - 1; i >= 0; --i)
        {
            if (IsElement(itemList[i]))
                return i;
        }

        return -1;
    }

    // ---- Minify
    // Separate from pretty-print.  Shares NormalizeMultilineComment with the rest; otherwise its own recursion.

    private void MinifyTopLevel(IEnumerable<JsonItem> docModel, IBuffer buffer)
    {
        _buffer = buffer;
        _pads = new PaddedFormattingTokens(Options, StringLengthFunc);

        var atStartOfNewLine = true;
        foreach (var item in docModel)
            atStartOfNewLine = MinifyItem(item, atStartOfNewLine);

        _buffer = new NullBuffer();
    }

    /// <summary>
    /// Recursively write a minified version of the item to the buffer, while preserving comments.
    /// </summary>
    private bool MinifyItem(JsonItem item, bool atStartOfNewLine)
    {
        var newline = "\n";
        _buffer.Add(item.PrefixComment);
        if (item.Name.Length > 0)
            _buffer.Add(item.Name, ":");

        if (item.MiddleComment.Contains(newline))
        {
            var normalizedComment = NormalizeMultilineComment(item.MiddleComment, int.MaxValue);
            foreach(var line in normalizedComment)
                _buffer.Add(line, newline);
        }
        else
        {
            _buffer.Add(item.MiddleComment);
        }

        if (item.Type is JsonItemType.Array or JsonItemType.Object)
        {
            var (openBracket, closeBracket) = (item.Type is JsonItemType.Object) ? ("{", "}") : ("[", "]");
            _buffer.Add(openBracket);

            // Loop through children.  Print commas when needed.  Keep track of when we've started a new line -
            // that's important for blank lines.
            var needsComma = false;
            atStartOfNewLine = false;
            foreach (var child in item.Children)
            {
                if (IsElement(child))
                {
                    if (needsComma)
                        _buffer.Add(",");
                    needsComma = true;
                }
                atStartOfNewLine = MinifyItem(child, atStartOfNewLine);
            }
            _buffer.Add(closeBracket);
        }
        else if (item.Type is JsonItemType.BlankLine)
        {
            // Make sure we're starting on a new line before inserting a blank line.  Otherwise, some can be lost.
            if (!atStartOfNewLine)
                _buffer.Add(newline);
            _buffer.Add(newline);
            return true;
        }
        else if (item.Type is JsonItemType.LineComment)
        {
            // Make sure we start on a new line for the comment, so that it will definitely be parsed as standalone.
            if (!atStartOfNewLine)
                _buffer.Add(newline);
            _buffer.Add(item.Value, newline);
            return true;
        }
        else if (item.Type is JsonItemType.BlockComment)
        {
            // Make sure we start on a new line for the comment, so that it will definitely be parsed as standalone.
            if (!atStartOfNewLine)
                _buffer.Add(newline);

            if (item.Value.Contains(newline))
            {
                var normalizedComment = NormalizeMultilineComment(item.Value, item.InputPosition.Column);
                foreach(var line in normalizedComment)
                    _buffer.Add(line, newline);
                return true;
            }
            else
            {
                _buffer.Add(item.Value, newline);
                return true;
            }
        }
        else
        {
            _buffer.Add(item.Value);
        }

        _buffer.Add(item.PostfixComment);
        if (item.PostfixComment.Length>0 && item.IsPostCommentLineStyle)
        {
            _buffer.Add(newline);
            return true;
        }

        return false;
    }
}
