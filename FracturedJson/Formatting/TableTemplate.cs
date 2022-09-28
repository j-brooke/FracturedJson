using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FracturedJson.Formatting;

/// <summary>
/// Collects spacing information about the columns of a potential table.  Each TableTemplate corresponds do
/// a part of a row, and they're nested recursively to match the JSON structure.
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
    public JsonItemType Type { get; private set; } = JsonItemType.Null;
    
    /// <summary>
    /// Assessment of whether this is a viable column.  The main qualifying factor is that all corresponding pieces
    /// of each row are the same type.
    /// </summary>
    public bool IsRowDataCompatible { get; private set; } = true;
    public int RowCount { get; private set; }
    
    public int NameLength { get; private set; }
    public int ValueLength { get; private set; }
    public int PrefixCommentLength { get; private set; }
    public int MiddleCommentLength { get; private set; }
    public int PostfixCommentLength { get; private set; }
    public BracketPaddingType PadType { get; private set; } = BracketPaddingType.Simple;
    public bool IsFormattableNumber { get; private set; }
    
    /// <summary>
    /// If this TableTemplate corresponds to an object or array, Children contains sub-templates
    /// for the array/object's children.
    /// </summary>
    public IList<TableTemplate> Children { get; set; } = new List<TableTemplate>();

    public TableTemplate(PaddedFormattingTokens pads, bool allowReformattingNumbers)
    {
        _pads = pads;
        _allowReformattingNumbers = allowReformattingNumbers;
        IsFormattableNumber = allowReformattingNumbers;
    }

    /// <summary>
    /// Analyzes an object/array for formatting as a potential table.  The tableRoot is a container that
    /// is split out across many lines.  Each "row" is a single child written inline.
    /// </summary>
    public void MeasureTableRoot(JsonItem tableRoot)
    {
        IsRowDataCompatible = (tableRoot.Type is JsonItemType.Array or JsonItemType.Object);
        if (!IsRowDataCompatible)
            return;
        
        // For each row of the potential table, measure it and its children, making room for everything.
        // (Or, if there are incompatible types at any level, set CanBeUsedInTable to false.)
        foreach(var child in tableRoot.Children)
            MeasureRowSegment(child);

        PruneUnusableSegments(int.MaxValue);

        // If there are fewer than 2 actual data rows (i.e., not standalone comments), no point making a table.
        IsRowDataCompatible &= (RowCount >= 2);
    }

    public bool TryToFit(int maximumLength)
    {
        for (var complexity = GetTemplateComplexity(); complexity > 0; --complexity)
        {
            if (ComputeSize() <= maximumLength)
                return true;
            PruneUnusableSegments(complexity-1);
        }

        return false;
    }

    /// <summary>
    /// Returns the of the table-formatted items.  It's a method rather than a precomputed static value because
    /// we might want to remove sub-templates before we're finished.  This is only valid after
    /// <see cref="MeasureTableRoot"/> has been called.
    /// </summary>
    public int ComputeSize()
    {
        var actualValueSize = ValueLength;
        if (Children.Count>0)
        {
            actualValueSize = Children.Sum(ch => ch.ComputeSize())
                              + Math.Max(0, _pads.CommaLen * (Children.Count - 1))
                              + _pads.ArrStartLen(PadType)
                              + _pads.ArrEndLen(PadType);
        }
        else if (IsFormattableNumber)
        {
            actualValueSize = _maxDigitsBeforeDecimal + _maxDigitsAfterDecimal + ((_maxDigitsAfterDecimal > 0) ? 1 : 0);
            if (_dataContainsNull && actualValueSize < 4)
                actualValueSize = 4;
        }

        return ((PrefixCommentLength > 0) ? PrefixCommentLength + _pads.CommentLen : 0)
               + ((NameLength > 0) ? NameLength + _pads.ColonLen : 0)
               + ((MiddleCommentLength > 0) ? MiddleCommentLength + _pads.CommentLen : 0)
               + actualValueSize
               + ((PostfixCommentLength > 0) ? PostfixCommentLength + _pads.CommentLen : 0);
    }

    public string FormatNumber(string originalValueString)
    {
        if (!IsFormattableNumber)
            throw new FracturedJsonException("Logic error - attempting to format inappropriate thing as number");

        if (_numberFormat == null)
        {
            var totalWidth = _maxDigitsBeforeDecimal + _maxDigitsAfterDecimal + ((_maxDigitsAfterDecimal > 0) ? 1 : 0);
            if (_dataContainsNull && totalWidth < 4)
                totalWidth = 4;
            _numberFormat = $"{{0,{totalWidth}:F{_maxDigitsAfterDecimal}}}";
        }

        return string.Format(CultureInfo.InvariantCulture, _numberFormat, double.Parse(originalValueString));
    }

    private readonly PaddedFormattingTokens _pads;
    private readonly bool _allowReformattingNumbers;
    private int _maxDigitsBeforeDecimal = 0;
    private int _maxDigitsAfterDecimal = 0;
    private string? _numberFormat;
    private bool _dataContainsNull = false;

    private void MeasureRowSegment(JsonItem rowSegment)
    {
        // Comments and blank lines don't figure into template measurements
        if (rowSegment.Type is JsonItemType.BlankLine or JsonItemType.BlockComment or JsonItemType.LineComment)
            return;
        
        // Make sure the type of this row is compatible with what we've seen already.  Null is 
        // compatible with everything.
        if (rowSegment.Type is JsonItemType.False or JsonItemType.True)
        {
            IsRowDataCompatible = (Type is JsonItemType.True or JsonItemType.Null);
            Type = JsonItemType.True;
            IsFormattableNumber = false;
        }
        else if (rowSegment.Type is JsonItemType.Number)
        {
            IsRowDataCompatible = (Type is JsonItemType.Number or JsonItemType.Null);
            Type = JsonItemType.Number;
        }
        else if (rowSegment.Type is JsonItemType.Null)
        {
            _dataContainsNull = true;
        }
        else
        {
            IsRowDataCompatible = (Type == rowSegment.Type || Type == JsonItemType.Null);
            if (Type is JsonItemType.Null)
                Type = rowSegment.Type;
            IsFormattableNumber = false;
        }

        // If multiple lines are necessary for a row (probably due to pesky comments), we can't make a table.
        IsRowDataCompatible &= !rowSegment.RequiresMultipleLines;

        // Looks good.  Update the numbers.
        RowCount += 1;
        NameLength = Math.Max(NameLength, rowSegment.NameLength);
        ValueLength = Math.Max(ValueLength, rowSegment.ValueLength);
        MiddleCommentLength = Math.Max(MiddleCommentLength, rowSegment.MiddleCommentLength);
        PrefixCommentLength = Math.Max(PrefixCommentLength, rowSegment.PrefixCommentLength);
        PostfixCommentLength = Math.Max(PostfixCommentLength, rowSegment.PostfixCommentLength);

        if (rowSegment.Complexity >= 2)
            PadType = BracketPaddingType.Complex;

        if (!IsRowDataCompatible)
            return;
        
        if (rowSegment.Type == JsonItemType.Array)
        {
            // For each row in this rowSegment, find or create this TableTemplate's child template for
            // the that array index, and then measure recursively.
            for (var i = 0; i < rowSegment.Children.Count; ++i)
            {
                if (Children.Count <= i)
                    Children.Add(new(_pads, _allowReformattingNumbers));
                var subTemplate = Children[i];
                subTemplate.MeasureRowSegment(rowSegment.Children[i]);
            }
        }
        else if (rowSegment.Type == JsonItemType.Object)
        {
            // If this object has multiple children with the same property name, which is allowed by the JSON standard
            // although it's hard to imagine anyone would deliberately do it, we can't format it as part of a table.
            var distinctChildKeyCount = rowSegment.Children.Select(item => item.Name)
                .Distinct()
                .Count();
            if (distinctChildKeyCount != rowSegment.Children.Count)
            {
                IsRowDataCompatible = false;
                return;
            }

            // For each property in rowSegment, check whether there's sub-template with the same name.  If not
            // found, create one.  Then measure recursively.
            foreach (var rowSegChild in rowSegment.Children)
            {
                var subTemplate = Children.FirstOrDefault(tt => tt.LocationInParent == rowSegChild.Name);
                if (subTemplate == null)
                {
                    subTemplate = new(_pads, _allowReformattingNumbers) { LocationInParent = rowSegChild.Name };
                    Children.Add(subTemplate);
                }
                subTemplate.MeasureRowSegment(rowSegChild);
            }
        }
        else if (rowSegment.Type == JsonItemType.Number && IsFormattableNumber)
        {
            const int maxChars = 15;
            var normalizedVal = double.Parse(rowSegment.Value).ToString("G", CultureInfo.InvariantCulture);
            IsFormattableNumber = normalizedVal.Length <= maxChars && !normalizedVal.Contains('E');

            var indexOfDot = normalizedVal.IndexOf('.');
            _maxDigitsBeforeDecimal =
                Math.Max(_maxDigitsBeforeDecimal, (indexOfDot >= 0) ? indexOfDot : normalizedVal.Length);
            _maxDigitsAfterDecimal =
                Math.Max(_maxDigitsAfterDecimal, (indexOfDot >= 0) ? normalizedVal.Length - indexOfDot - 1 : 0);
        }
    }

    /// <summary>
    /// If our sub-templates aren't viable, get rid of them.
    /// </summary>
    private void PruneUnusableSegments(int maxAllowedComplexity)
    {
        if (maxAllowedComplexity<=0)
            Children.Clear();
        
        foreach(var subTemplate in Children)
            subTemplate.PruneUnusableSegments(maxAllowedComplexity-1);
        
        if (!IsRowDataCompatible)
            Children.Clear();
    }

    private int GetTemplateComplexity()
    {
        if (Children.Count == 0)
            return 0;
        return 1 + Children.Max(ch => ch.GetTemplateComplexity());
    }
}
