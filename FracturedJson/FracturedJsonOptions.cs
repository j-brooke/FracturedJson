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
    /// Maximum length that the formatter can use when combining complex elements into a single line.  This
    /// includes comments, property names, etc. - everything except indentation and any PrefixString.  Note that
    /// lines containing only a single element can exceed this: a long string, or an element with a long prefix
    /// or postfix comment, for example.
    /// <seealso cref="MaxTotalLineLength"/>
    /// </summary>
    public int MaxInlineLength { get; set; } = int.MaxValue;

    /// <summary>
    /// Maximum length that the formatter can use when combining complex elements into a single line, from the start
    /// of the line.  This is identical to <see cref="MaxInlineLength"/> except that this one DOES count indentation
    /// and any PrefixString.
    /// <seealso cref="MaxInlineLength"/>
    /// </summary>
    public int MaxTotalLineLength { get; set; } = 120;

    /// <summary>
    /// Maximum degree of nesting of arrays/objects that may be written on a single line.  0 disables inlining (but see
    /// related settings).  1 allows inlining of arrays/objects that contain only simple items.  2 allows inlining of
    /// arrays/objects that contain other arrays/objects as long as the child containers only contain simple items.  Etc.
    /// <seealso cref="MaxCompactArrayComplexity"/>
    /// <seealso cref="MaxTableRowComplexity"/>
    /// </summary>
    public int MaxInlineComplexity { get; set; } = 2;

    /// <summary>
    /// Maximum degree of nesting of arrays formatted as with multiple items per row across multiple rows.
    /// <seealso cref="MaxInlineComplexity"/>
    /// <seealso cref="MaxTableRowComplexity"/>
    /// </summary>
    public int MaxCompactArrayComplexity { get; set; } = 1;

    /// <summary>
    /// Maximum degree of nesting of arrays/objects formatted as table rows.
    /// <seealso cref="MaxCompactArrayComplexity"/>
    /// <seealso cref="MaxInlineComplexity"/>
    /// </summary>
    public int MaxTableRowComplexity { get; set; } = 2;

    // TODO: document
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
    /// If true, there won't be any spaces or tabs at the end of lines.  Normally there are a variety of cases where
    /// whitespace can be created or preserved at the ends of lines.  The most noticeable case is when
    /// <see cref="CommaPadding"/> is true.  Setting this to true gets rid of all of that (including inside multi-
    /// line comments).
    /// </summary>
    public bool OmitTrailingWhitespace { get; set; } = false;

    /// <summary>
    /// Controls how lists or columns of numbers (possibly with nulls) are aligned, and whether their precision
    /// may be normalized.
    /// </summary>
    public NumberListAlignment NumberListAlignment { get; set; } = NumberListAlignment.Normalize;

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
}
