using System;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Throws a new FracturedJsonException unconditionally.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <exception cref="FracturedJsonException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(string message)
    {
        throw new FracturedJsonException(message);
    }

    /// <summary>
    /// Throws a new FracturedJsonException if the condition is true.
    /// </summary>
    /// <param name="condition">True if the exception should be thrown</param>
    /// <param name="message">The message that describes the error</param>
    /// <exception cref="FracturedJsonException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf(bool condition, string message)
    {
        if (condition)
            Throw(message);
    }

    /// <summary>
    /// Throws a FracturedJsonException unconditionally, noting the location in the input at which
    /// the problem occurred.  The location is available as both a property/data and appended to the message.
    /// </summary>
    /// <param name="message">The message that describes the error.  (The inputPosition will be appended automatically
    /// </param>
    /// <param name="inputPosition">Location in the input text where the error occurred.</param>
    /// <exception cref="FracturedJsonException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(string message, InputPosition inputPosition)
    {
        throw Create(message, inputPosition);
    }

    /// <summary>
    /// Throws a FracturedJsonException if the condition is true, noting the location in the input at which
    /// the problem occurred.  The location is available as both a property/data and appended to the message.
    /// </summary>
    /// <param name="condition">True if the exception should be thrown</param>
    /// <param name="message">The message that describes the error.  (The inputPosition will be appended automatically
    /// </param>
    /// <param name="inputPosition">Location in the input text where the error occurred.</param>
    /// <exception cref="FracturedJsonException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf(bool condition, string message, InputPosition inputPosition)
    {
        if (condition)
            Throw(message, inputPosition);
    }
}
