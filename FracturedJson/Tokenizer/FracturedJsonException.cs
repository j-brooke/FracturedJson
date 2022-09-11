using System;

namespace FracturedJson.Tokenizer;

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
}
