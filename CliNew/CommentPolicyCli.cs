using FracturedJson;

namespace CliNew;

/// <summary>
/// The same as <see cref="CommentPolicy"/>, except with some short aliases as well.
/// </summary>
public enum CommentPolicyCli
{
    TreatAsError = CommentPolicy.TreatAsError,
    Remove = CommentPolicy.Remove,
    Preserve = CommentPolicy.Preserve,
    E = TreatAsError,
    R = Remove,
    P = Preserve
}
