using FracturedJson;

namespace CliNew;

public class CliSettings
{
    public FileInfo? InputFile { get; set; }
    public FileInfo? OutputFile { get; set; }
    public CliReturn? ImmediateExitReturnCode { get; set; }
    public bool Minify { get; set; }
    public bool WritePerformanceInfo { get; set; }
    public bool EastAsianWideChars { get; set; }
    public FracturedJsonOptions FjOptions { get; set; } = new();
}
