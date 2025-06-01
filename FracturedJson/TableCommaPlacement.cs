namespace FracturedJson;

/// <summary>
/// Specifies where commas should be in table-formatted elements.
/// </summary>
public enum TableCommaPlacement
{
    /// <summary>
    /// Commas come right after the element that comes before them.
    /// </summary>
    BeforePadding,

    /// <summary>
    /// Commas come after the column padding, all lined with each other.
    /// </summary>
    AfterPadding,

    /// <summary>
    /// Commas come right after the element, except in the case of columns of numbers.
    /// </summary>
    BeforePaddingExceptNumbers,
}
