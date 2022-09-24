namespace FracturedJson.V3;

public record struct FracturedJsonOptions
{
    public EolStyle JsonEolStyle { get; set; } = EolStyle.Default;
    public int MaxInlineLength { get; set; } = 80;
    public int MaxTotalLineLength { get; set; } = 120;
    public int MaxInlineComplexity { get; set; } = 2;
    public int MaxCompactArrayComplexity { get; set; } = 1;
    
    /// <summary>
    /// If an inlined array or object contains other arrays or objects, setting NestedBracketPadding to true
    /// will include spaces inside the outer brackets.
    /// <seealso cref="SimpleBracketPadding"/>
    /// </summary>
    /// <remarks>
    /// Example: <br/>
    /// true: [ [1, 2, 3], [4] ] <br/>
    /// false: [[1, 2, 3], [4]] <br/>
    /// </remarks>
    public bool NestedBracketPadding { get; set; } = true;

    /// <summary>
    /// If an inlined array or object does NOT contain other arrays/objects, setting SimpleBracketPadding to true
    /// will include spaces inside the brackets.
    /// <seealso cref="NestedBracketPadding"/>
    /// </summary>
    public bool SimpleBracketPadding { get; set; } = false;

    /// <summary>
    /// If true, includes a space after property colons.
    /// </summary>
    public bool ColonPadding { get; set; } = true;

    /// <summary>
    /// If true, includes a space after commas separating array items and object properties.
    /// </summary>
    public bool CommaPadding { get; set; } = true;

    /// <summary>
    /// If true, spaces are included between prefix and postfix comments and their content.
    /// </summary>
    public bool CommentPadding { get; set; } = true;
    
    /// <summary>
    /// If true, numbers won't be right-aligned with matching precision.
    /// </summary>
    public bool DontJustifyNumbers { get; set; } = false;

    /// <summary>
    /// Number of spaces to use per indent level (unless UseTabToIndent is true)
    /// </summary>
    public int IndentSpaces { get; set; } = 4;

    /// <summary>
    /// Uses a single tab per indent level, instead of spaces.
    /// </summary>
    public bool UseTabToIndent { get; set; } = false;
    
    /// <summary>
    /// String attached to the beginning of every line, before regular indentation.
    /// </summary>
    public string PrefixString { get; set; } = string.Empty;
    
    public CommentPolicy CommentPolicy { get; set; } = CommentPolicy.TreatAsError;
    public bool PreserveBlankLines { get; set; } = false;
    public bool AllowTrailingCommas { get; set; } = false;
    
    public FracturedJsonOptions()
    {}
}
