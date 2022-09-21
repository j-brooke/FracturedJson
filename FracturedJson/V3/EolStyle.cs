namespace FracturedJson.V3;

/// <summary>
/// Specifies what sort of line endings to use.
/// </summary>
public enum EolStyle
{
    /// <summary>
    /// The native environment's line endings will be used.
    /// </summary>
    Default,

    /// <summary>
    /// Carriage Return, followed by a line feed.  Windows-style.
    /// </summary>
    Crlf,

    /// <summary>
    /// Just a line feed.  Unix-style (including Mac).
    /// </summary>
    Lf,
}
