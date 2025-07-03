namespace WebFormatter2.Components;

/// <summary>
/// Stores the user's view preferences for local storage.
/// </summary>
public record ViewOptions
{
    /// <summary>
    /// Show the settings sidebar with all of the <see cref="FracturedJson.FracturedJsonOptions"/> properties.
    /// </summary>
    public bool ShowSettings { get; set; } = true;

    public ViewMode ViewMode { get; set; } = ViewMode.OverUnder;

    public bool DarkTheme { get; set; }
}
