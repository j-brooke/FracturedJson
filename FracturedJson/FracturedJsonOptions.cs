namespace FracturedJson;

/// <summary>
/// Settings controlling the output of FracturedJson-formatted JSON documents.
/// </summary>
public record FracturedJsonOptions
{
    /// <summary>
    /// Dictates which characters to use for line breaks.
    /// </summary>
    public EolStyle JsonEolStyle { get; set; } = EolStyle.Default;

    /// <summary>
    /// Maximum length that the formatter can use when combining complex elements into a single line, from the start
    /// of the line.  (It's still possible for individual values to exceed this length.  For example, a long string.)
    /// </summary>
    public int MaxTotalLineLength { get; set; } = 120;

    /// <summary>
    /// Maximum degree of nesting of arrays/objects that may be written on a single line.  0 disables inlining (but see
    /// related settings).  1 allows inlining of arrays/objects that contain only simple items.  2 allows inlining of
    /// arrays/objects that contain other arrays/objects as long as the child containers only contain simple items. Etc.
    /// <seealso cref="MaxCompactArrayComplexity"/>
    /// <seealso cref="MaxTableRowComplexity"/>
    /// </summary>
    public int MaxInlineComplexity { get; set; } = 2;

    /// <summary>
    /// Maximum degree of nesting of arrays formatted as with multiple items per row across multiple rows.  Use 0
    /// to disable multi-line arrays with multiple items per line.
    /// <seealso cref="MaxInlineComplexity"/>
    /// <seealso cref="MaxTableRowComplexity"/>
    /// <seealso cref="MinCompactArrayRowItems"/>
    /// </summary>
    public int MaxCompactArrayComplexity { get; set; } = 2;

    /// <summary>
    /// Maximum degree of nesting of arrays/objects formatted as table rows.  0 only allows a single column of simple
    /// types or empty arrays/objects to be formatted as a table. When set to 1, each row can be an array or object
    /// containing only simple types.  2 allows arrays/objects that contain other arrays/objects that contain only
    /// simple types.  Etc.
    /// <seealso cref="MaxCompactArrayComplexity"/>
    /// <seealso cref="MaxInlineComplexity"/>
    /// </summary>
    public int MaxTableRowComplexity { get; set; } = 2;

    /// <summary>
    /// Maximum size difference between property name lengths in an object to qualify for property alignment
    /// in expanded objects (not tables).
    /// </summary>
    public int MaxPropNamePadding { get; set; } = 16;

    /// <summary>
    /// When lining up object properties on different lines, if true, the colon will come before the padding spaces
    /// (right next to the property name); if false, the colons will be after the padding, all lined up with each
    /// other.  This applies to table and expanded formatting.
    /// </summary>
    public bool ColonBeforePropNamePadding { get; set; } = false;

    /// <summary>
    /// Determines whether commas in table-formatted elements are lined up in their own column or right next to the
    /// element that precedes them.
    /// </summary>
    public TableCommaPlacement TableCommaPlacement { get; set; } = TableCommaPlacement.BeforePadding;

    /// <summary>
    /// Minimum number of items allowed per row to format an array as with multiple items per line across multiple
    /// lines.  This is an approximation, not a hard rule.  The idea is that if there will be too few items per row,
    /// you'd probably rather see it as a table.
    /// </summary>
    public int MinCompactArrayRowItems { get; set; } = 3;

    /// <summary>
    /// Depth at which lists/objects are always fully expanded, regardless of other settings.
    /// -1 = none; 0 = root node only; 1 = root node and its children.
    /// </summary>
    public int AlwaysExpandDepth { get; set; } = -1;

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
    /// Controls how lists or columns of numbers (possibly with nulls) are aligned, and whether their precision
    /// may be normalized.  When set to <see cref="NumberListAlignment.Normalize"/>, numbers will be rewritten to
    /// with the same number of digits after a decimal place as their peers.  Other values preserve the numbers exactly
    /// as they're written in the input document.
    /// </summary>
    public NumberListAlignment NumberListAlignment { get; set; } = NumberListAlignment.Decimal;

    /// <summary>
    /// Number of spaces to use per indent level.  If <see cref="UseTabToIndent"/> is true, spaces won't be used but
    /// this number will still be used in length computations.
    /// <seealso cref="UseTabToIndent"/>
    /// </summary>
    public int IndentSpaces { get; set; } = 4;

    /// <summary>
    /// Uses a single tab per indent level, instead of spaces.
    /// <seealso cref="IndentSpaces"/>
    /// </summary>
    public bool UseTabToIndent { get; set; } = false;
    
    /// <summary>
    /// String attached to the beginning of every line, before regular indentation.  If this string contains anything
    /// other than whitespace, this will probably make the output invalid JSON, but it might be useful for output
    /// to documentation, for instance.
    /// </summary>
    public string PrefixString { get; set; } = string.Empty;

    /// <summary>
    /// Determines how the parser and formatter should treat comments.  The JSON standard does not allow comments,
    /// but it's a common unofficial extension.  (Such files are often given the extension ".jsonc".)
    /// </summary>
    public CommentPolicy CommentPolicy { get; set; } = CommentPolicy.TreatAsError;

    /// <summary>
    /// If true, blank lines in the original input should be preserved in the output.
    /// </summary>
    public bool PreserveBlankLines { get; set; } = false;

    /// <summary>
    /// If true, arrays and objects that contain a comma after their last element are permitting.  The JSON standard
    /// does not allow commas after the final element of an array or object, but some systems permit it, so
    /// it's nice to have the option here.
    /// </summary>
    public bool AllowTrailingCommas { get; set; } = false;

    /// <summary>
    /// Returns a new <see cref="FracturedJsonOptions"/> object with the recommended default settings without concern
    /// for backward compatibility.  The constructor's defaults should preserve the same behavior from one minor
    /// revision to the next even if new features are added.  The instance created by this method will be updated
    /// with new settings if they are more sensible for most cases.
    /// </summary>
    public static FracturedJsonOptions Recommended()
    {
        // At the beginning of version 5, the defaults are the recommended settings.  This may change in future
        // minor versions.
        return new FracturedJsonOptions();
    }
}
