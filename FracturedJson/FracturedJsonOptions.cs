namespace FracturedJson;

/// <summary>
/// Settings controlling the output of FracturedJson-formatted JSON documents.
/// </summary>
public record FracturedJsonOptions
{
    /// <summary>
    /// Specifies the line break style (e.g., LF or CRLF) for the formatted JSON output. See <see cref="EolStyle"/>
    /// for options.
    /// </summary>
    public EolStyle JsonEolStyle { get; set; } = EolStyle.Default;

    /// <summary>
    /// Maximum length (in characters, including indentation) when more than one simple value is put on a line.
    /// Individual values (e.g., long strings) may exceed this limit.
    /// </summary>
    public int MaxTotalLineLength { get; set; } = 120;

    /// <summary>
    /// Maximum nesting level of arrays/objects that may be written on a single line.  0 disables inlining (but see
    /// related settings).  1 allows inlining of arrays/objects that contain only simple items.  2 allows inlining of
    /// arrays/objects that contain other arrays/objects as long as the child containers only contain simple items.
    /// Higher values allow deeper nesting.
    /// <seealso cref="MaxCompactArrayComplexity"/>
    /// <seealso cref="MaxTableRowComplexity"/>
    /// </summary>
    public int MaxInlineComplexity { get; set; } = 2;

    /// <summary>
    /// Maximum nesting level for arrays formatted with multiple items per row across multiple lines. Set to 0 to
    /// disable this format.  1 allows arrays containing only simple values to be formatted this way.  2 allows arrays
    /// containing arrays or elements that contain only simple values.  Higher values allow deeper nesting.
    /// <seealso cref="MaxInlineComplexity"/>
    /// <seealso cref="MaxTableRowComplexity"/>
    /// <seealso cref="MinCompactArrayRowItems"/>
    /// </summary>
    public int MaxCompactArrayComplexity { get; set; } = 2;

    /// <summary>
    /// Maximum nesting level of the rows of an array or object formatted as a table with aligned columns.  When set
    /// to 0, the rows may only be simple values and there will only be one column.  When set to 1, each row can be
    /// an array or object containing only simple values.  Higher values allow deeper nesting.
    /// <seealso cref="MaxCompactArrayComplexity"/>
    /// <seealso cref="MaxInlineComplexity"/>
    /// </summary>
    public int MaxTableRowComplexity { get; set; } = 2;

    /// <summary>
    /// Maximum length difference between property names in an object to align them vertically in expanded (non-table)
    /// formatting.
    /// </summary>
    public int MaxPropNamePadding { get; set; } = 16;

    /// <summary>
    /// If true, colons in aligned object properties are placed right after the property name (e.g., 'name:    value');
    /// if false, colons align vertically after padding (e.g., 'name   : value'). Applies to table and expanded
    /// formatting.
    /// </summary>
    public bool ColonBeforePropNamePadding { get; set; } = false;

    /// <summary>
    /// Determines whether commas in table-formatted rows are lined up in their own column after padding or placed
    /// directly after each element, before padding spaces.
    /// </summary>
    public TableCommaPlacement TableCommaPlacement { get; set; } = TableCommaPlacement.BeforePadding;

    /// <summary>
    /// Minimum items per row to format an array with multiple items per line across multiple lines.  This is a
    /// guideline, not a strict rule.
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
    /// <remarks>
    /// Example: <br/>
    /// true: [ [ 1, 2, 3 ], [ 4 ] ]
    /// false: [ [1, 2, 3], [4] ] <br/>
    /// </remarks>
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
    /// If true, spaces are included between JSON data and comments that precede or follow them on the same line.
    /// </summary>
    public bool CommentPadding { get; set; } = true;

    /// <summary>
    /// Controls alignment of numbers in table columns or compact multiline arrays.  When set to
    /// <see cref="NumberListAlignment.Normalize"/>, numbers are rewritten to have the same decimal precision as others
    /// in the same column.  Other settings preserve input numbers exactly.
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
    /// If true, allows a comma after the last element in arrays or objects, which is non-standard JSON but supported
    /// by some systems.
    /// </summary>
    public bool AllowTrailingCommas { get; set; } = false;

    /// <summary>
    /// Creates a new <see cref="FracturedJsonOptions"/> with recommended settings, prioritizing sensible defaults
    /// over backward compatibility. Constructor defaults maintain consistent behavior across minor versions, while
    /// this method may adopt newer, preferred settings.
    /// </summary>
    public static FracturedJsonOptions Recommended()
    {
        // At the beginning of version 5, the defaults are the recommended settings.  This may change in future
        // minor versions.
        return new FracturedJsonOptions();
    }
}
