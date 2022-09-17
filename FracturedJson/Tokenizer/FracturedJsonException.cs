using System;

namespace FracturedJson.Tokenizer;

/// <summary>
/// Exception indicating something went wrong while processing JSON data.
/// </summary>
public class FracturedJsonException : Exception
{
    public InputPosition?  InputPosition { get; }
    
    public FracturedJsonException()
    {}

    public FracturedJsonException(string message)
        : base(message)
    {}

    public FracturedJsonException(string message, InputPosition inputPosition)
        : base(message)
    {
        InputPosition = inputPosition;
    }

    public static FracturedJsonException Create(string message, InputPosition inputPosition)
    {
        var newMessage = $"{message} at idx={inputPosition.Index}, row={inputPosition.Row}, col={inputPosition.Column}";
        return new FracturedJsonException(newMessage, inputPosition);
    }
}
