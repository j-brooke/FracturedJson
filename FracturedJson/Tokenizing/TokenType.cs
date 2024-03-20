namespace FracturedJson.Tokenizing;

/// <summary>
/// Types of tokens that can be read from a stream of JSON text.  Comments aren't part of the official JSON
/// standard, but we're supporting them anyway.  BlankLine isn't typically a token by itself, but we want to
/// try to preserve those.
/// </summary>
public enum TokenType
{
    /// <summary>
    /// A character or sequence that doesn't make sense in a JSON document.
    /// </summary>
    Invalid,

    /// <summary>
    /// Open square bracket: [
    /// </summary>
    BeginArray,

    /// <summary>
    /// Close square bracket: ]
    /// </summary>
    EndArray,

    /// <summary>
    /// Open curly bracket: {
    /// </summary>
    BeginObject,

    /// <summary>
    /// Close curly bracket: }
    /// </summary>
    EndObject,

    /// <summary>
    /// Quotation marks and the characters between them.
    /// </summary>
    String,

    /// <summary>
    /// Digits, maybe a sign or a decimal point, occasionally an "e".
    /// </summary>
    Number,

    /// <summary>
    /// The keyword null.
    /// </summary>
    Null,

    /// <summary>
    /// The keyword true.
    /// </summary>
    True,

    /// <summary>
    /// The keyword false.
    /// </summary>
    False,

    /// <summary>
    /// A comment beginning with slash-star and ending with star-slack.
    /// </summary>
    BlockComment,

    /// <summary>
    /// A comment beginning with two slashes and continuing to the end of the line.
    /// </summary>
    LineComment,

    /// <summary>
    /// A line with no characters, or only whitespace.
    /// </summary>
    BlankLine,

    /// <summary>
    /// The symbol ","
    /// </summary>
    Comma,

    /// <summary>
    /// The symbol ":"
    /// </summary>
    Colon,
}
