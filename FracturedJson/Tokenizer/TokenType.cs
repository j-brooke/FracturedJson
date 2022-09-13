namespace FracturedJson.Tokenizer;

/// <summary>
/// Types of tokens that can be read from a stream of JSON text.  Comments aren't part of the official JSON
/// standard, but we're supporting them anyway.  BlankLine isn't typically a token by itself, but we want to
/// try to preserve those.
/// </summary>
public enum TokenType
{
    Invalid,
    BeginArray,
    EndArray,
    BeginObject,
    EndObject,
    String,
    Number,
    Null,
    True,
    False,
    BlockComment,
    LineComment,
    BlankLine,
    Comma,
    Colon,
}
