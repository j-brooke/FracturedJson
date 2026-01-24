using FracturedJson;

namespace CliNew;

/// <summary>
/// Same as <see cref="NumberListAlignment"/> but with some extra shorter aliases for the values.
/// </summary>
public enum NumberListAlignmentCli
{
    Left = NumberListAlignment.Left,
    Right = NumberListAlignment.Right,
    Decimal = NumberListAlignment.Decimal,
    Normalize = NumberListAlignment.Normalize,
    L = Left,
    R = Right,
    D = Decimal,
    N = Normalize,
}
