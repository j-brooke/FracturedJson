using Blazored.LocalStorage;
using FracturedJson;
using Wcwidth;

namespace WebFormatter2.Components;

public class WebFormatterState
{
    public event Action? SomethingHappened;
    public FracturedJsonOptions Options { get; set; } = new();
    public string InputJson {
        get => _inputJson;
        set
        {
            if (_inputJson == value)
                return;
            _inputJson = value;
            Console.WriteLine($"InputJson updated to: {value}");
            SomethingHappened?.Invoke();
        }
    }

    public string OutputJson
    {
        get => _outputJson;
        set
        {
            if (_outputJson == value)
                return;
            _outputJson = value;
            SomethingHappened?.Invoke();
        }
    }

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

        // Migrate old settings with the new fixed value here.
        if (restoredOpts != null)
            restoredOpts.OmitTrailingWhitespace = true;

        Options = restoredOpts ?? GetDefaultOptions();
    }

    public void SaveOptionsToLocalStorage()
    {
        _localStorage.SetItem(_optionsKey, Options);
    }

    private const string _optionsKey = "options";
    private readonly Formatter _formatter;
    private readonly ISyncLocalStorageService _localStorage;
    private string _inputJson = string.Empty;
    private string _outputJson = string.Empty;

    private static FracturedJsonOptions GetDefaultOptions()
    {
        return new()
        {
            MaxInlineLength = 500000,
            MaxTotalLineLength = 100,
            CommentPolicy = CommentPolicy.Preserve,
            PreserveBlankLines = true,
            OmitTrailingWhitespace = true,
            TableCommaPlacement = TableCommaPlacement.BeforePadding,
        };
    }

    public static int WideCharStringLength(string str)
    {
        return str.EnumerateRunes().Sum(rune => UnicodeCalculator.GetWidth(rune.Value));
    }
}
