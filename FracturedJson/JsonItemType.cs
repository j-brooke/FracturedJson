namespace FracturedJson;

/// <summary>
/// Type of a piece of a JSON document including comments and blank lines.
/// </summary>
public enum JsonItemType
{
    /// <summary>
    /// The literal value "null"
    /// </summary>
    Null,

    /// <summary>
    /// The literal value "false"
    /// </summary>
    False,

    /// <summary>
    /// The literal value "true"
    /// </summary>
    True,

    /// <summary>
    /// A bunch of characters between quotes.
    /// </summary>
    String,

    /// <summary>
    /// A number, possibly in scientific notation.
    /// </summary>
    Number,

    /// <summary>
    /// An object - a collection of key/value pairs
    /// </summary>
    Object,

    /// <summary>
    /// An array - a list of values
    /// </summary>
    Array,

    /// <summary>
    /// A line with nothing but whitespace.
    /// </summary>
    BlankLine,

    /// <summary>
    /// A comment beginning with two slashes and continuing to the end of the line.
    /// </summary>
    LineComment,

    /// <summary>
    /// A comment starting with slash star and ending with star slash.
    /// </summary>
    BlockComment,
}
