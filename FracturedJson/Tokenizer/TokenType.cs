namespace FracturedJson.Tokenizer;

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