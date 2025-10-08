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

    public TableColumnType Type { get; private set; } = TableColumnType.Unknown;
    public int RowCount { get; private set; }
    
    public int NameLength { get; private set; }
    public int NameMinimum { get; private set; } = int.MaxValue;
    public int SimpleValueLength { get; private set; }
    public int PrefixCommentLength { get; private set; }
    public int MiddleCommentLength { get; private set; }
    public int PostfixCommentLength { get; private set; }
    public bool IsAnyPostCommentLineStyle { get; set; }
    public BracketPaddingType PadType { get; private set; } = BracketPaddingType.Simple;

    /// <summary>
    /// True if this is a number column and we're allowed by settings to normalize numbers (rewrite them with the same
    /// precision), and if none of the numbers have too many digits or require scientific notation.
    /// </summary>
    public bool AllowNumberNormalization { get; private set; }

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
        AllowNumberNormalization = (numberListAlignment == NumberListAlignment.Normalize);
    }

    /// <summary>
    /// Analyzes an object/array for formatting as a potential table.  The tableRoot is a container that
    /// is split out across many lines.  Each "row" is a single child written inline.
    /// </summary>
    public void MeasureTableRoot(JsonItem tableRoot, bool recursive)
    {
        // For each row of the potential table, measure it and its children, making room for everything.
        // (Or, if there are incompatible types at any level, set CanBeUsedInTable to false.)
        foreach(var child in tableRoot.Children)
            MeasureRowSegment(child, recursive);

        // Get rid of incomplete junk and figure out our size.
        PruneAndRecompute(int.MaxValue);
    }

    /// <summary>
    /// Check if the template's width fits in the given size.  Repeatedly drop inner formatting and
    /// recompute to make it fit, if needed.
    /// </summary>
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
        var formatType = (_numberListAlignment is NumberListAlignment.Normalize && !AllowNumberNormalization)
            ? NumberListAlignment.Left
            : _numberListAlignment;

        // The easy cases.  Use the value exactly as it was in the source doc.
        switch (formatType)
        {
            case NumberListAlignment.Left:
                buffer.Add(item.Value, commaBeforePadType, _pads.Spaces(SimpleValueLength - item.ValueLength));
                return;
            case NumberListAlignment.Right:
                buffer.Add(_pads.Spaces(SimpleValueLength - item.ValueLength), item.Value, commaBeforePadType);
                return;
        }

        // Normalize case - rewrite the number with the appropriate precision.
        if (formatType is NumberListAlignment.Normalize)
        {
            if (item.Type is JsonItemType.Null)
            {
                buffer.Add(_pads.Spaces(_maxDigBeforeDecNorm - item.ValueLength), item.Value,
                    commaBeforePadType, _pads.Spaces(CompositeValueLength - _maxDigBeforeDecNorm));
                return;
            }

            // Create a .NET format string, if we don't already have one.
            _numberFormat ??= $"{{0,{CompositeValueLength}:F{_maxDigAfterDecNorm}}}";

            var parsedVal = double.Parse(item.Value, CultureInfo.InvariantCulture);
            var reformattedStr = string.Format(CultureInfo.InvariantCulture, _numberFormat, parsedVal);
            buffer.Add(reformattedStr, commaBeforePadType);
            return;
        }

        // Decimal case - line up the decimals (or E's) but leave the value exactly as it was in the source.
        if (item.Type is JsonItemType.Null)
        {
            buffer.Add(_pads.Spaces(_maxDigBeforeDecRaw - item.ValueLength), item.Value,
                commaBeforePadType, _pads.Spaces(CompositeValueLength - _maxDigBeforeDecRaw));
            return;
        }

        int leftPad;
        int rightPad;
        var indexOfDot = item.Value.IndexOfAny(_dotOrE);
        if (indexOfDot > 0)
        {
            leftPad = _maxDigBeforeDecRaw - indexOfDot;
            rightPad = CompositeValueLength - leftPad - item.ValueLength;
        }
        else
        {
            leftPad = _maxDigBeforeDecRaw - item.ValueLength;
            rightPad = CompositeValueLength - _maxDigBeforeDecRaw;
        }

        buffer.Add(_pads.Spaces(leftPad), item.Value, commaBeforePadType, _pads.Spaces(rightPad));
    }

    private static readonly char[] _dotOrE = new[] { '.', 'e', 'E' };
    private readonly PaddedFormattingTokens _pads;
    private readonly NumberListAlignment _numberListAlignment;
    private int _maxDigBeforeDecRaw = 0;
    private int _maxDigAfterDecRaw = 0;
    private int _maxDigBeforeDecNorm = 0;
    private int _maxDigAfterDecNorm = 0;
    private string? _numberFormat;

    // Regex to help us distinguish between numbers that truly have a zero value - which can take many forms like
    // 0, 0.000, and 0.0e75 - and numbers too small for a 64bit float, such as 1e-500.
    private static readonly Regex _trulyZeroValString = new Regex("^-?[0.]+([eE].*)?$");

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
            _maxDigBeforeDecNorm = Math.Max(_maxDigBeforeDecNorm, _pads.LiteralNullLen);
            _maxDigBeforeDecRaw = Math.Max(_maxDigBeforeDecRaw, _pads.LiteralNullLen);
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
        SimpleValueLength = Math.Max(SimpleValueLength, rowSegment.ValueLength);
        MiddleCommentLength = Math.Max(MiddleCommentLength, rowSegment.MiddleCommentLength);
        PrefixCommentLength = Math.Max(PrefixCommentLength, rowSegment.PrefixCommentLength);
        PostfixCommentLength = Math.Max(PostfixCommentLength, rowSegment.PostfixCommentLength);
        IsAnyPostCommentLineStyle |= rowSegment.IsPostCommentLineStyle;

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
            // If this object has multiple children with the same property name, which is allowed by the JSON standard
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
        else if (Type is TableColumnType.Number)
        {
            // So far, everything in this column is a number (or null).  We need to reevaluate whether we're allowed
            // to normalize the numbers - write them all with the same number of digits after the decimal point.
            // We also need to take some measurements for both contingencies.
            const int maxChars = 15;
            var parsedVal = double.Parse(rowSegment.Value, CultureInfo.InvariantCulture);
            var normalizedStr = parsedVal.ToString("G", CultureInfo.InvariantCulture);

            // JSON allows numbers that won't fit in a 64-bit double.  For example, 1e500 becomes Infinity, and
            // 1e-500 becomes 0.  In either of those cases, we shouldn't try to reformat the column.  Likewise,
            // if there are too many digits or it needs to be expressed in scientific notation, we're better off
            // not even trying.
            AllowNumberNormalization &= !double.IsNaN(parsedVal)
                                  && !double.IsInfinity(parsedVal)
                                  && normalizedStr.Length <= maxChars
                                  && !normalizedStr.Contains('E')
                                  && (parsedVal!=0.0 || _trulyZeroValString.IsMatch(rowSegment.Value));

            // Measure the number of digits before and after the decimal point if we write it as a standard,
            // non-scientific notation number.
            var indexOfDotNorm = normalizedStr.IndexOf('.');
            _maxDigBeforeDecNorm =
                Math.Max(_maxDigBeforeDecNorm, (indexOfDotNorm >= 0) ? indexOfDotNorm : normalizedStr.Length);
            _maxDigAfterDecNorm =
                Math.Max(_maxDigAfterDecNorm, (indexOfDotNorm >= 0) ? normalizedStr.Length - indexOfDotNorm - 1 : 0);

            // Measure the number of digits before and after the decimal point (or E scientific notation with not
            // decimal point), using the number exactly as it was in the input document.
            var indexOfDotRaw = rowSegment.Value.IndexOfAny(_dotOrE);
            _maxDigBeforeDecRaw =
                Math.Max(_maxDigBeforeDecRaw, (indexOfDotRaw >= 0) ? indexOfDotRaw : rowSegment.ValueLength);
            _maxDigAfterDecRaw =
                Math.Max(_maxDigAfterDecRaw, (indexOfDotRaw >= 0) ? rowSegment.ValueLength - indexOfDotRaw - 1 : 0);
        }

        AllowNumberNormalization &= (Type is TableColumnType.Number);
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
            CompositeValueLength = SimpleValueLength;
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
        if (_numberListAlignment == NumberListAlignment.Normalize && AllowNumberNormalization)
        {
            var normDecLen = (_maxDigAfterDecNorm > 0) ? 1 : 0;
            return _maxDigBeforeDecNorm + normDecLen + _maxDigAfterDecNorm;
        }
        else if (_numberListAlignment == NumberListAlignment.Decimal)
        {
            var rawDecLen = (_maxDigAfterDecRaw > 0) ? 1 : 0;
            return _maxDigBeforeDecRaw + rawDecLen + _maxDigAfterDecRaw;
        }

        return SimpleValueLength;
    }
}
