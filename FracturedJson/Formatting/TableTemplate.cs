using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FracturedJson.Formatting;

/// <summary>
/// Collects spacing information about the columns of a potential table.  Each TableTemplate corresponds to
/// a part of a row, and they're nested recursively to match the JSON structure.  (Also used in complex multiline
/// arrays to try to fit them all nicely together.)
/// </summary>
/// <remarks>
/// <para> Say you have an object/array where each item would make a nice row all by itself.  We want to try to line up 
/// everything about it - comments, prop names, values.  If the row items are themselves objects/arrays, ideally
/// we'd like to line up all of their children, too, recursively.  This only works as long as the structure/types
/// are consistent.</para>
/// </remarks>
internal class TableTemplate
{
    /// <summary>
    /// The property name in the table that this segment matches up with. 
    /// </summary>
    public string? LocationInParent { get; private set; }

    /// <summary>
    /// Type of the column, for table formatting purposes.  Numbers have special options.  Arrays or objects can
    /// have recursive sub-columns.  If they're other simple types or if there's a mix of types, we basically
    /// treat them as strings (no recursion).
    /// </summary>
    public TableColumnType Type { get; private set; } = TableColumnType.Unknown;
    public int RowCount { get; private set; }

    /// <summary>
    /// Length of the longest property name.
    /// </summary>
    public int NameLength { get; private set; }

    /// <summary>
    /// Length of the shortest property name.
    /// </summary>
    public int NameMinimum { get; private set; } = int.MaxValue;

    /// <summary>
    /// Largest length for the value parts of the column, not counting any table formatting padding.
    /// </summary>
    public int MaxValueLength { get; private set; }

    /// <summary>
    /// Length of the largest value that can't be split apart; i.e., values other than arrays and objects.
    /// </summary>
    public int MaxAtomicValueLength { get; private set; }
    public int PrefixCommentLength { get; private set; }
    public int MiddleCommentLength { get; private set; }
    public bool AnyMiddleCommentHasNewline { get; private set; }
    public int PostfixCommentLength { get; private set; }
    public bool IsAnyPostCommentLineStyle { get; private set; }
    public BracketPaddingType PadType { get; private set; } = BracketPaddingType.Simple;
    public bool RequiresMultipleLines { get; private set; }

    /// <summary>
    /// Length of the value for this template when things are complicated.  For arrays and objects, it's the sum of
    /// all the child templates' lengths, plus brackets and commas and such.  For number lists, it's the space
    /// required to align them as appropriate.
    /// </summary>
    public int CompositeValueLength { get; private set; }

    /// <summary>
    /// Length of the entire template, including space for the value, property name, and all comments.
    /// </summary>
    public int TotalLength { get; private set; }

    /// <summary>
    /// If the row contains non-empty array or objects whose value is shorter than the literal null, an extra adjustment
    /// is needed.
    /// </summary>
    public int ShorterThanNullAdjustment { get; private set; }

    /// <summary>
    /// True if at least one row in the column this represents has a null value.
    /// </summary>
    public bool ContainsNull { get; private set; }

    /// <summary>
    /// If this TableTemplate corresponds to an object or array, Children contains sub-templates
    /// for the array/object's children.
    /// </summary>
    public IList<TableTemplate> Children { get; set; } = new List<TableTemplate>();

    public TableTemplate(PaddedFormattingTokens pads, NumberListAlignment numberListAlignment)
    {
        _pads = pads;
        _numberListAlignment = numberListAlignment;
    }

    /// <summary>
    /// <para>Analyzes an object/array for formatting as a table, formatting as a compact multiline array, or
    /// formatting as an expanded object with aligned properties.  In the first two cases, we measure recursively so
    /// that values nested inside arrays and objects can be aligned too.</para>
    /// <para>The item given is presumed to require multiple lines.  For table formatting, each of its children is
    /// expected to be one row.  For compact multiline arrays, there will be multiple children per line, but they
    /// will be given the same amount of space and lined up neatly when spanning multiple lines.  For expanded
    /// object properties, the values may or may not span multiple lines, but the property names and the start of
    /// their values will be on separate lines, lined up.</para>
    /// </summary>
    public void MeasureTableRoot(JsonItem tableRoot, bool recursive)
    {
        // For each row of the potential table, measure it and its children, making room for everything.
        // (Or, if there are incompatible types at any level, set CanBeUsedInTable to false.)
        foreach(var child in tableRoot.Children)
            MeasureRowSegment(child, recursive);

        // Get rid of incomplete junk and determine our final size.
        PruneAndRecompute(int.MaxValue);
    }

    /// <summary>
    /// Check if the template's width fits in the given size.  Repeatedly drop inner formatting and
    /// recompute to make it fit, if needed.
    /// </summary>
    /// <example>
    /// Fully expanded, an array might look like this when table-formatted.
    /// <code>
    /// [
    ///     { "a": 3.4, "b":   8, "c": {"x": 2, "y": 16        } },
    ///     { "a": 2,   "b": 301, "c": {        "y": -4, "z": 0} }
    /// ]
    /// </code>
    /// If that's too wide, the template will give up trying to align the x,y,z properties but keep the rest.
    /// <code>
    /// [
    ///     { "a": 3.4, "b":   8, "c": {"x": 2, "y": 16} },
    ///     { "a": 2,   "b": 301, "c": {"y": -4, "z": 0} }
    /// ]
    /// </code>
    /// </example>
    public bool TryToFit(int maximumLength)
    {
        for (var complexity = GetTemplateComplexity(); complexity >= 1; --complexity)
        {
            if (TotalLength <= maximumLength)
                return true;
            PruneAndRecompute(complexity-1);
        }

        return false;
    }

    /// <summary>
    /// Added the number, properly aligned and possibly reformatted, according to our measurements.
    /// This assumes that the segment is a number list, and therefore that the item is a number or null.
    /// </summary>
    public void FormatNumber(IBuffer buffer, JsonItem item, string commaBeforePadType)
    {
        // The easy cases.  Use the value exactly as it was in the source doc.
        switch (_numberListAlignment)
        {
            case NumberListAlignment.Left:
                buffer.Add(item.Value, commaBeforePadType, _pads.Spaces(MaxValueLength - item.ValueLength));
                return;
            case NumberListAlignment.Right:
                buffer.Add(_pads.Spaces(MaxValueLength - item.ValueLength), item.Value, commaBeforePadType);
                return;
        }

        if (item.Type is JsonItemType.Null)
        {
            buffer.Add(_pads.Spaces(_maxDigBeforeDec - item.ValueLength), item.Value,
                commaBeforePadType, _pads.Spaces(CompositeValueLength - _maxDigBeforeDec));
            return;
        }

        // Normalize case - rewrite the number with the appropriate precision.
        if (_numberListAlignment is NumberListAlignment.Normalize)
        {
             // Create a .NET format string, if we don't already have one.
             _numberFormat ??= "F" + _maxDigAfterDec;

            var parsedVal = double.Parse(item.Value, _invarFormatProvider);
            var reformattedStr = parsedVal.ToString(_numberFormat, _invarFormatProvider);
            buffer.Add(_pads.Spaces(CompositeValueLength - reformattedStr.Length), reformattedStr, commaBeforePadType);
            return;
        }

        // Decimal case - line up the decimals (or E's) but leave the value exactly as it was in the source.
        int leftPad;
        int rightPad;
        var indexOfDot = item.Value.IndexOfAny(_dotOrE);
        if (indexOfDot > 0)
        {
            leftPad = _maxDigBeforeDec - indexOfDot;
            rightPad = CompositeValueLength - leftPad - item.ValueLength;
        }
        else
        {
            leftPad = _maxDigBeforeDec - item.ValueLength;
            rightPad = CompositeValueLength - _maxDigBeforeDec;
        }

        buffer.Add(_pads.Spaces(leftPad), item.Value, commaBeforePadType, _pads.Spaces(rightPad));
    }

    /// <summary>
    /// Length of the largest item - including property name, comments, and padding - that can't be split across
    /// multiple lines.
    /// </summary>
    public int AtomicItemSize()
    {
        return NameLength
               + _pads.ColonLen
               + MiddleCommentLength
               + ((MiddleCommentLength > 0) ? _pads.CommentLen : 0)
               + MaxAtomicValueLength
               + PostfixCommentLength
               + ((PostfixCommentLength > 0) ? _pads.CommentLen : 0)
               + _pads.CommaLen;
    }

    // Regex to help us distinguish between numbers that truly have a zero value - which can take many forms like
    // 0, 0.000, and 0.0e75 - and numbers too small for a 64bit float, such as 1e-500.
    private static readonly Regex _trulyZeroValString = new Regex("^-?[0.]+([eE].*)?$");

    private static readonly IFormatProvider _invarFormatProvider = CultureInfo.InvariantCulture;
    private static readonly char[] _dotOrE = new[] { '.', 'e', 'E' };

    private readonly PaddedFormattingTokens _pads;
    private NumberListAlignment _numberListAlignment;
    private int _maxDigBeforeDec = 0;
    private int _maxDigAfterDec = 0;
    private string? _numberFormat;

    /// <summary>
    /// Adjusts this TableTemplate (and its children) to make room for the given rowSegment (and its children).
    /// </summary>
    /// <param name="rowSegment"></param>
    /// <param name="recursive">true if the measurement should include children for arrays/objects.</param>
    private void MeasureRowSegment(JsonItem rowSegment, bool recursive)
    {
        // Standalone comments and blank lines don't figure into template measurements
        if (rowSegment.Type is JsonItemType.BlankLine or JsonItemType.BlockComment or JsonItemType.LineComment)
            return;

        var rowTableType = rowSegment.Type switch
        {
            JsonItemType.Null => TableColumnType.Unknown,
            JsonItemType.Number => TableColumnType.Number,
            JsonItemType.Array => TableColumnType.Array,
            JsonItemType.Object => TableColumnType.Object,
            _ => TableColumnType.Simple,
        };

        if (Type is TableColumnType.Unknown)
            Type = rowTableType;
        else if (rowTableType is not TableColumnType.Unknown && Type != rowTableType)
            Type = TableColumnType.Mixed;

        if (rowSegment.Type is JsonItemType.Null)
        {
            _maxDigBeforeDec = Math.Max(_maxDigBeforeDec, _pads.LiteralNullLen);
            ContainsNull = true;
        }

        if (rowSegment.RequiresMultipleLines)
        {
            RequiresMultipleLines = true;
            Type = TableColumnType.Mixed;
        }

        // Update the numbers.
        RowCount += 1;
        NameLength = Math.Max(NameLength, rowSegment.NameLength);
        NameMinimum = Math.Min(NameMinimum, rowSegment.NameLength);
        MaxValueLength = Math.Max(MaxValueLength, rowSegment.ValueLength);
        MiddleCommentLength = Math.Max(MiddleCommentLength, rowSegment.MiddleCommentLength);
        PrefixCommentLength = Math.Max(PrefixCommentLength, rowSegment.PrefixCommentLength);
        PostfixCommentLength = Math.Max(PostfixCommentLength, rowSegment.PostfixCommentLength);
        IsAnyPostCommentLineStyle |= rowSegment.IsPostCommentLineStyle;
        AnyMiddleCommentHasNewline |= rowSegment.MiddleCommentHasNewline;

        if (rowSegment.Type is not (JsonItemType.Array or JsonItemType.Object))
            MaxAtomicValueLength = Math.Max(MaxAtomicValueLength, rowSegment.ValueLength);

        if (rowSegment.Complexity >= 2)
            PadType = BracketPaddingType.Complex;

        if (RequiresMultipleLines || rowSegment.Type is JsonItemType.Null)
            return;
        
        if (Type is TableColumnType.Array && recursive)
        {
            // For each row in this rowSegment, find or create this TableTemplate's child template for
            // the that array index, and then measure recursively.
            for (var i = 0; i < rowSegment.Children.Count; ++i)
            {
                if (Children.Count <= i)
                    Children.Add(new(_pads, _numberListAlignment));
                var subTemplate = Children[i];
                subTemplate.MeasureRowSegment(rowSegment.Children[i], true);
            }
        }
        else if (Type is TableColumnType.Object && recursive)
        {
            // If this object has multiple children with the same property name, which is allowed by the JSON standard,
            // although it's hard to imagine anyone would deliberately do it, we can't format it as part of a table.
            var distinctChildKeyCount = rowSegment.Children.Select(item => item.Name)
                .Distinct()
                .Count();
            if (distinctChildKeyCount != rowSegment.Children.Count)
            {
                Type = TableColumnType.Simple;
                return;
            }

            // For each property in rowSegment, check whether there's sub-template with the same name.  If not
            // found, create one.  Then measure recursively.
            foreach (var rowSegChild in rowSegment.Children)
            {
                var subTemplate = Children.FirstOrDefault(tt => tt.LocationInParent == rowSegChild.Name);
                if (subTemplate == null)
                {
                    subTemplate = new(_pads, _numberListAlignment) { LocationInParent = rowSegChild.Name };
                    Children.Add(subTemplate);
                }
                subTemplate.MeasureRowSegment(rowSegChild, true);
            }
        }

        // The rest is only relevant to number columns were we plan to align the decimal points.
        if (Type is not TableColumnType.Number
            || _numberListAlignment is (NumberListAlignment.Left or NumberListAlignment.Right))
            return;

        // For Decimal, we use the string exactly as it is from the input document.  For Normalize, we need to rewrite
        // it before we count digits.
        var normalizedStr = rowSegment.Value;
        if (_numberListAlignment is NumberListAlignment.Normalize)
        {
            const int maxChars = 16;
            var parsedVal = double.Parse(rowSegment.Value, _invarFormatProvider);
            normalizedStr = parsedVal.ToString("G", _invarFormatProvider);

            // Normalize only works for numbers that can be faithfully represented without too many digits and without
            // scientific notation.  The JSON standard allows numbers of any length/precision.  If we detect any case
            // where we'd lose precision, fall back to left alignment for this column.
            var canNormalize = !double.IsNaN(parsedVal)
                               && !double.IsInfinity(parsedVal)
                               && normalizedStr.Length <= maxChars
                               && !normalizedStr.Contains('E')
                               && (parsedVal != 0.0 || _trulyZeroValString.IsMatch(rowSegment.Value));
            if (!canNormalize)
            {
                _numberListAlignment = NumberListAlignment.Left;
                return;
            }
        }

        var indexOfDot = normalizedStr.IndexOfAny(_dotOrE);
        _maxDigBeforeDec = Math.Max(_maxDigBeforeDec, (indexOfDot >= 0) ? indexOfDot : normalizedStr.Length);
        _maxDigAfterDec = Math.Max(_maxDigAfterDec, (indexOfDot >= 0) ? normalizedStr.Length - indexOfDot - 1 : 0);
    }

    /// <summary>
    /// Get rid of any sub-templates we don't want - either because we found the row data wasn't compatible after all,
    /// or because we need to reduce size.  Recompute CompositeValueLength and TotalLength, which may have changed
    /// because of the pruning (or may not have been set yet at all).
    /// </summary>
    private void PruneAndRecompute(int maxAllowedComplexity)
    {
        if (maxAllowedComplexity <= 0 || (Type is not (TableColumnType.Array or TableColumnType.Object)) || RowCount<2)
            Children.Clear();
        
        foreach(var subTemplate in Children)
            subTemplate.PruneAndRecompute(maxAllowedComplexity-1);

        if (Type is TableColumnType.Number)
        {
            CompositeValueLength = GetNumberFieldWidth();
        }
        else if (Children.Count > 0)
        {
            CompositeValueLength = Children.Sum(ch => ch.TotalLength)
                                   + Math.Max(0, _pads.CommaLen * (Children.Count - 1))
                                   + _pads.ArrStartLen(PadType)
                                   + _pads.ArrEndLen(PadType);
            if (ContainsNull && CompositeValueLength < _pads.LiteralNullLen)
            {
                ShorterThanNullAdjustment = _pads.LiteralNullLen - CompositeValueLength;
                CompositeValueLength = _pads.LiteralNullLen;
            }
        }
        else
        {
            CompositeValueLength = MaxValueLength;
        }

        TotalLength = ((PrefixCommentLength > 0) ? PrefixCommentLength + _pads.CommentLen : 0)
                    + ((NameLength > 0) ? NameLength + _pads.ColonLen : 0)
                    + ((MiddleCommentLength > 0) ? MiddleCommentLength + _pads.CommentLen : 0)
                    + CompositeValueLength
                    + ((PostfixCommentLength > 0) ? PostfixCommentLength + _pads.CommentLen : 0);
    }

    private int GetTemplateComplexity()
    {
        if (Children.Count == 0)
            return 0;
        return 1 + Children.Max(ch => ch.GetTemplateComplexity());
    }

    private int GetNumberFieldWidth()
    {
        if (_numberListAlignment is (NumberListAlignment.Normalize or NumberListAlignment.Decimal))
        {
            var rawDecLen = (_maxDigAfterDec > 0) ? 1 : 0;
            return _maxDigBeforeDec + rawDecLen + _maxDigAfterDec;
        }

        return MaxValueLength;
    }
}
