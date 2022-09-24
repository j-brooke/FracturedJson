using System;
using System.Collections.Generic;
using System.Linq;

namespace FracturedJson.V3;

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
public class TableTemplate
{
    /// <summary>
    /// The property name in the table that this segment matches up with. 
    /// </summary>
    public string? LocationInParent { get; set; }
    public JsonItemType Type { get; set; } = JsonItemType.Null;
    
    /// <summary>
    /// Assessment of whether this is a viable column.  The main qualifying factor is that all corresponding pieces
    /// of each row are the same type.
    /// </summary>
    public bool CanBeUsedInTable { get; set; } = true;
    public int RowCount { get; set; }
    
    public int NameLength { get; set; }
    public int ValueLength { get; set; }
    public int PrefixCommentLength { get; set; }
    public int MiddleCommentLength { get; set; }
    public int PostfixCommentLength { get; set; }
    public BracketPaddingType PadType { get; set; } = BracketPaddingType.Empty;
    
    /// <summary>
    /// If this TableTemplate corresponds to an object or array, Children contains sub-templates
    /// for the array/object's children.
    /// </summary>
    public IList<TableTemplate> Children { get; set; } = new List<TableTemplate>();

    /// <summary>
    /// Analyzes an object/array for formatting as a potential table.  The tableRoot is a container that
    /// is split out across many lines.  Each "row" is a single child written inline.
    /// </summary>
    public void MeasureTableRoot(JsonItem tableRoot)
    {
        CanBeUsedInTable = (tableRoot.Type is JsonItemType.Array or JsonItemType.Object);
        if (!CanBeUsedInTable)
            return;
        
        // For each row of the potential table, measure it and its children, making room for everything.
        // (Or, if there are incompatible types at any level, set CanBeUsedInTable to false.)
        foreach(var child in tableRoot.Children)
            MeasureRowSegment(child);

        PruneUnusableSegments();

        // If there are fewer than 2 actual data rows (i.e., not standalone comments), no point making a table.
        CanBeUsedInTable &= (RowCount >= 2);
    }

    /// <summary>
    /// Returns the of the table-formatted items.  It's a method rather than a precomputed static value because
    /// we might want to remove sub-templates before we're finished.
    /// </summary>
    public int ComputeSize(PaddedFormattingTokens pads)
    {
        var actualValueSize = ValueLength;
        if (Children.Count>0)
            actualValueSize = Children.Sum(ch => ch.ComputeSize(pads)) 
                        + Math.Max(0, pads.CommaLen * (Children.Count - 1))
                        + pads.ArrStartLen(BracketPaddingType.Complex)
                        + pads.ArrEndLen(BracketPaddingType.Complex);
        return ((PrefixCommentLength > 0) ? PrefixCommentLength + pads.CommentLen : 0)
               + ((NameLength > 0) ? NameLength + pads.ColonLen : 0)
               + MiddleCommentLength
               + actualValueSize
               + ((PostfixCommentLength > 0) ? PostfixCommentLength + pads.CommentLen : 0);
    }

    private void MeasureRowSegment(JsonItem rowSegment)
    {
        // If we're already disqualified, skip further logic.
        if (!CanBeUsedInTable)
            return;
        
        // Comments and blank lines don't figure into template measurements
        if (rowSegment.Type is JsonItemType.BlankLine or JsonItemType.BlockComment or JsonItemType.LineComment)
            return;
        
        // Make sure the type of this row is compatible with what we've seen already.  Null is 
        // compatible with everything.
        if (rowSegment.Type is JsonItemType.False or JsonItemType.True)
        {
            CanBeUsedInTable = (Type is JsonItemType.True or JsonItemType.Null);
            Type = JsonItemType.True;
        }
        else if (rowSegment.Type is not JsonItemType.Null)
        {
            CanBeUsedInTable = (Type == rowSegment.Type || Type == JsonItemType.Null);
            Type = rowSegment.Type;
        }

        // If multiple lines are necessary for a row (probably due to pesky comments), we can't make a table.
        CanBeUsedInTable &= !rowSegment.RequiresMultipleLines;
        
        if (!CanBeUsedInTable)
            return;
        
        // Looks good.  Update the numbers.
        RowCount += 1;
        NameLength = Math.Max(NameLength, rowSegment.NameLength);
        ValueLength = Math.Max(ValueLength, rowSegment.ValueLength);
        MiddleCommentLength = Math.Max(MiddleCommentLength, rowSegment.MiddleCommentLength);
        PrefixCommentLength = Math.Max(PrefixCommentLength, rowSegment.PrefixCommentLength);
        PostfixCommentLength = Math.Max(PostfixCommentLength, rowSegment.PostfixCommentLength);

        if (rowSegment.Type == JsonItemType.Array)
        {
            // For each row in this rowSegment, find or create this TableTemplate's child template for
            // the that array index, and then measure recursively.
            for (var i = 0; i < rowSegment.Children.Count; ++i)
            {
                if (Children.Count <= i)
                    Children.Add(new());
                var subTemplate = Children[i];
                subTemplate.MeasureRowSegment(rowSegment.Children[i]);
            }
        }
        else if (rowSegment.Type == JsonItemType.Object)
        {
            // For each property in rowSegment, check whether there's sub-template with the same name.  If not
            // found, create one.  Then measure recursively.
            foreach (var rowSegChild in rowSegment.Children)
            {
                var subTemplate = Children.FirstOrDefault(tt => tt.LocationInParent == rowSegChild.Name);
                if (subTemplate == null)
                {
                    subTemplate = new() { LocationInParent = rowSegChild.Name };
                    Children.Add(subTemplate);
                }
                subTemplate.MeasureRowSegment(rowSegChild);
            }
        }
    }

    /// <summary>
    /// If our sub-templates aren't viable, get rid of them.
    /// </summary>
    private void PruneUnusableSegments()
    {
        foreach(var subTemplate in Children)
            subTemplate.PruneUnusableSegments();
        var hasUnusable = !Children.All(ch => ch.CanBeUsedInTable);
        if (hasUnusable)
            Children.Clear();
    }
}
