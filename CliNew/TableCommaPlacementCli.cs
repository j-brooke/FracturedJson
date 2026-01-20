using System.Diagnostics.CodeAnalysis;
using FracturedJson;

namespace CliNew;

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
