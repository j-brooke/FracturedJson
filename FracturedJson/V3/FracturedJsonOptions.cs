namespace FracturedJson.V3;

public record struct FracturedJsonOptions
{
    public int MaxInlineLength { get; set; } = 80;
    public int MaxTotalLineLength { get; set; } = 120;
    public int MaxInlineComplexity { get; set; } = 2;
    public int MaxCompactArrayComplexity { get; set; } = 1;
    public CommentPolicy CommentPolicy { get; set; } = CommentPolicy.TreatAsError;
    public bool PreserveBlankLines { get; set; } = false;
    public bool AllowTrailingCommas { get; set; } = false;
    
    public FracturedJsonOptions()
    {}
}
