using FracturedJson;

namespace CliNew;

public enum CommentPolicyCli
{
    TreatAsError = CommentPolicy.TreatAsError,
    Remove = CommentPolicy.Remove,
    Preserve = CommentPolicy.Preserve,
    E = TreatAsError,
    R = Remove,
    P = Preserve
}
