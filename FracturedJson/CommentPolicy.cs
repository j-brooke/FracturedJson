namespace FracturedJson;

/// <summary>
/// Instructions on what to do about comments found in the input text.  According to the JSON standard, comments
/// aren't allowed.  But "JSON with comments" is pretty wide-spread these days, thanks largely to Microsoft,
/// so it's nice to have options.
/// </summary>
public enum CommentPolicy
{
    /// <summary>
    /// An exception will be thrown if comments are found in the input.
    /// </summary>
    TreatAsError,

    /// <summary>
    /// Comments are allowed in the input, but won't be included in the output.
    /// </summary>
    Remove,

    /// <summary>
    /// Comments found in the input should be included in the output.
    /// </summary>
    Preserve,
}
