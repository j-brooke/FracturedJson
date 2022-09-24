using System;
using System.Collections.Generic;
using System.Linq;

namespace FracturedJson.V3;

public class TableTemplate
{
    public string? LocationInParent { get; set; }
    public JsonItemType Type { get; set; } = JsonItemType.Null;
    public bool CanBeUsedInTable { get; set; } = true;
    public int RowCount { get; set; }
    
    public int NameLength { get; set; }
    public int ValueLength { get; set; }
    public int PrefixCommentLength { get; set; }
    public int MiddleCommentLength { get; set; }
    public int PostfixCommentLength { get; set; }
    public BracketPaddingType PadType { get; set; } = BracketPaddingType.Empty;

    public IList<TableTemplate> Children { get; set; } = new List<TableTemplate>();

    public void AssessTableRoot(JsonItem tableRoot)
    {
        CanBeUsedInTable = (tableRoot.Type is JsonItemType.Array or JsonItemType.Object);
        if (!CanBeUsedInTable)
            return;
        
        foreach(var child in tableRoot.Children)
            AssessRowSegment(child);

        PruneUnusableSegments();

        // If there are fewer than 2 actual data rows (i.e., not standalone comments), no point making a table.
        CanBeUsedInTable &= (RowCount >= 2);
    }

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

    private void AssessRowSegment(JsonItem rowSegment)
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
            Type = rowSegment.Type;
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
            for (var i = 0; i < rowSegment.Children.Count; ++i)
            {
                if (Children.Count <= i)
                    Children.Add(new());
                var subTemplate = Children[i];
                subTemplate.AssessRowSegment(rowSegment.Children[i]);
            }
        }
        else if (rowSegment.Type == JsonItemType.Object)
        {
            foreach (var rowSegChild in rowSegment.Children)
            {
                var subTemplate = Children.FirstOrDefault(tt => tt.LocationInParent == rowSegChild.Name);
                if (subTemplate == null)
                {
                    subTemplate = new() { LocationInParent = rowSegChild.Name };
                    Children.Add(subTemplate);
                }
                subTemplate.AssessRowSegment(rowSegChild);
            }
        }
    }

    private void PruneUnusableSegments()
    {
        foreach(var subTemplate in Children)
            subTemplate.PruneUnusableSegments();
        var hasUnusable = !Children.All(ch => ch.CanBeUsedInTable);
        if (hasUnusable)
            Children.Clear();
    }
}
