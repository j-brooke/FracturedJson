namespace FracturedJson.Formatting;

/// <summary>
/// The data type represented by a <see cref="TableTemplate"/>, or "Mixed".
/// </summary>
internal enum TableColumnType
{
    /// <summary>
    /// Initial value.  Not useful by itself.
    /// </summary>
    Unknown,

    /// <summary>
    /// Non-container and non-number.  Could be a mix of strings, booleans, nulls, and/or numbers (but not all numbers).
    /// </summary>
    Simple,

    /// <summary>
    /// All values in the column are numbers or nulls.
    /// </summary>
    Number,

    /// <summary>
    /// All values in the column are arrays or nulls.
    /// </summary>
    Array,

    /// <summary>
    /// All values in the column are objects or nulls.
    /// </summary>
    Object,

    /// <summary>
    /// Multiple types in the column - for instance, a mix of arrays and strings.
    /// </summary>
    Mixed,
}
