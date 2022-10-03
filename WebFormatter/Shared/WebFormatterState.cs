using FracturedJson;

namespace WebFormatter.Shared;

public class WebFormatterState
{
    public FracturedJsonOptions Options { get; set; }
    public string InputJson { get; set; } = string.Empty;
    public string OutputJson { get; set; } = string.Empty;

    public WebFormatterState()
    {
        Options = new()
        {
            MaxInlineLength = 500000,
            MaxTotalLineLength = 100,
            CommentPolicy = CommentPolicy.Preserve,
            PreserveBlankLines = true,
        };
        _formatter = new() { Options = Options };
    }

    public void DoFormat()
    {
        try
        {
            _formatter.Options = Options;
            OutputJson = _formatter.Reformat(InputJson, 0);
        }
        catch (FracturedJsonException e)
        {
            OutputJson = e.Message;
        }
    }

    private readonly Formatter _formatter;
}
