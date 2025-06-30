using Blazored.LocalStorage;
using FracturedJson;
using Wcwidth;

namespace WebFormatter2.Components;

public class WebFormatterState : IDisposable, IAsyncDisposable
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

    public WebFormatterState(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
        _formatter = new() { StringLengthFunc = WideCharStringLength };

        // Periodically check for settings changes and send them to local storage if found.
        _timer = new(CheckSettingsBackup, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
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
    }

    public async Task RestoreOptionsFromLocalStorage()
    {
        var restoredOpts = await _localStorage.GetItemAsync<FracturedJsonOptions>(_optionsKey);
        Options = restoredOpts ?? GetDefaultOptions();
        _lastSavedOptions = Options with {};
        SomethingHappened?.Invoke();
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer != null) await _timer.DisposeAsync();
    }

    private const string _optionsKey = "options";
    private readonly Formatter _formatter;
    private readonly ILocalStorageService _localStorage;
    private Timer? _timer;
    private FracturedJsonOptions _lastSavedOptions = new();
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

    private void CheckSettingsBackup(object? state)
    {
        if (Options == _lastSavedOptions)
            return;

        _lastSavedOptions = Options with {};
        Task.Run(() => _localStorage.SetItemAsync(_optionsKey, Options));
    }

    private static int WideCharStringLength(string str)
    {
        return str.EnumerateRunes().Sum(rune => UnicodeCalculator.GetWidth(rune.Value));
    }
}
