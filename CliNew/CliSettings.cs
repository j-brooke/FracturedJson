using FracturedJson;

namespace CliNew;

/// <summary>
/// Set of all commandline settings, config file settings, and defaults
/// </summary>
public class CliSettings
{
    public FileInfo? InputFile { get; init; }
    public FileInfo? OutputFile { get; init; }
    public CliReturn? ImmediateExitReturnCode { get; init; }
    public bool Minify { get; init; }
    public bool WritePerformanceInfo { get; init; }
    public bool EastAsianWideChars { get; init; }
    public FracturedJsonOptions FjOptions { get; init; } = new();
}
