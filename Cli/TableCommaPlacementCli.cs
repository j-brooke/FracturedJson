using System.Diagnostics.CodeAnalysis;
using FracturedJson;

namespace Cli;

/// <summary>
/// Same as <see cref="TableCommaPlacement"/> but with some extra shorter aliases for the values.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum TableCommaPlacementCli
{
    BeforePadding = TableCommaPlacement.BeforePadding,
    AfterPadding = TableCommaPlacement.AfterPadding,
    BeforePaddingExceptNumbers = TableCommaPlacement.BeforePaddingExceptNumbers,
    BP = TableCommaPlacementCli.BeforePadding,
    AP = TableCommaPlacementCli.AfterPadding,
    BPEN = TableCommaPlacementCli.BeforePaddingExceptNumbers,
}
