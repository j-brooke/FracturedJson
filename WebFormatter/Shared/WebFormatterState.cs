using FracturedJson;

namespace WebFormatter.Shared;

public class WebFormatterState
{
    public FracturedJsonOptions Options { get; set; } = new();
    public string InputJson { get; set; } = string.Empty;
    public string OutputJson { get; set; } = string.Empty;
}
