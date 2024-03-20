using System;
using FracturedJson.Tokenizing;

namespace FracturedJson;

/// <summary>
/// Exception indicating something went wrong while processing JSON data.
/// </summary>
public class FracturedJsonException : Exception
{
    /// <summary>
    /// Location in the input at which the error occurred.
    /// </summary>
    public InputPosition?  InputPosition { get; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public FracturedJsonException()
    {}

    /// <summary>
    /// Constructor that takes a description.
    /// </summary>
    public FracturedJsonException(string message)
        : base(message)
    {}

    /// <summary>
    /// Constructor that takes a description and a position.
    /// </summary>
    public FracturedJsonException(string message, InputPosition inputPosition)
        : base(message)
    {
        InputPosition = inputPosition;
    }

    /// <summary>
    /// Constructor that takes a description, exception, and position.
    /// </summary>
    public FracturedJsonException(string message, Exception innerException, InputPosition inputPosition)
        : base(message, innerException)
    {
        InputPosition = inputPosition;
    }

    /// <summary>
    /// Generates a FracturedJsonException, appending a description of the position to the text.
    /// </summary>
    public static FracturedJsonException Create(string message, InputPosition inputPosition)
    {
        var newMessage = $"{message} at idx={inputPosition.Index}, row={inputPosition.Row}, col={inputPosition.Column}";
        return new FracturedJsonException(newMessage, inputPosition);
    }
}
