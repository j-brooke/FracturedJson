using Blazored.LocalStorage;
using FracturedJson;
using Wcwidth;

namespace WebFormatter.Shared;

public class WebFormatterState
{
    public FracturedJsonOptions Options { get; set; } = new();
    public string InputJson { get; set; } = string.Empty;
    public string OutputJson { get; set; } = string.Empty;

    public WebFormatterState(ISyncLocalStorageService localStorage)
    {
        _localStorage = localStorage;
        _formatter = new() { StringLengthFunc = WideCharStringLength };
    }

    public void DoFormat()
    {
        try
        {
            _formatter.Options = Options;
            OutputJson = _formatter.Reformat(InputJson, 0);
            SaveOptionsToLocalStorage();
        }
        catch (FracturedJsonException e)
        {
            OutputJson = e.Message;
        }
    }

    public void DoMinify()
    {
        try
        {
            _formatter.Options = Options;
            OutputJson = _formatter.Minify(InputJson);
        }
        catch (FracturedJsonException e)
        {
            OutputJson = e.Message;
        }
    }

    public void SetToDefaults()
    {
        Options = GetDefaultOptions();
        SaveOptionsToLocalStorage();
    }

    public void RestoreOptionsFromLocalStorage()
    {
        var restoredOpts = _localStorage.GetItem<FracturedJsonOptions>(_optionsKey);
        Options = restoredOpts ?? GetDefaultOptions();
    }

    public void SaveOptionsToLocalStorage()
    {
        _localStorage.SetItem(_optionsKey, Options);
    }

    private const string _optionsKey = "options";
    private readonly Formatter _formatter;
    private readonly ISyncLocalStorageService _localStorage;

    private static FracturedJsonOptions GetDefaultOptions()
    {
        return new()
        {
            MaxInlineLength = 500000,
            MaxTotalLineLength = 100,
            CommentPolicy = CommentPolicy.Preserve,
            PreserveBlankLines = true,
            OmitTrailingWhitespace = true,
        };
    }

    public static int WideCharStringLength(string str)
    {
        return str.EnumerateRunes().Sum(rune => UnicodeCalculator.GetWidth(rune.Value));
    }
}
