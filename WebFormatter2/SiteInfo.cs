using Microsoft.AspNetCore.Components;

namespace WebFormatter2;

/// <summary>
/// Distinguishes the official GitHub Pages site from a /preview/ build of the same app.
/// Detection follows the document base URI, which index.html sets from the URL path.
/// </summary>
public sealed class SiteInfo
{
    public const string OfficialFormatterUrl = "https://j-brooke.github.io/FracturedJson/";

    public SiteInfo(NavigationManager navigation)
    {
        IsPreview = PathLooksLikePreview(new Uri(navigation.BaseUri).AbsolutePath);
    }

    public bool IsPreview { get; }

    public static bool PathLooksLikePreview(string absolutePath)
    {
        var trimmed = absolutePath.TrimEnd('/');
        return trimmed.Equals("/preview", StringComparison.OrdinalIgnoreCase)
               || trimmed.EndsWith("/preview", StringComparison.OrdinalIgnoreCase);
    }
}
