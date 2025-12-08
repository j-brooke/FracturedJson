using Blazored.LocalStorage;
using FracturedJson;
using Microsoft.JSInterop;
using Wcwidth;

namespace WebFormatter2.Components;

public class WebFormatterState : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Event to tell components to update.
    /// </summary>
    public event Action? SomethingHappened;

    /// <summary>
    /// FracturedJson's settings.
    /// </summary>
    public FracturedJsonOptions Options { get; set; } = new();

    /// <summary>
    /// Webformatter-specific settings, like whether to hide or show the settings sidebar.
    /// </summary>
    public ViewOptions ViewOptions { get; set; } = new();

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

    public string CombinedJson
    {
        get => _combinedJson;
        set
        {
            if (_combinedJson == value)
                return;
            _combinedJson = value;
            SomethingHappened?.Invoke();
        }
    }

    public string StandaloneErrorMsg { get; set; } = string.Empty;

    public WebFormatterState(ILocalStorageService localStorage, IJSRuntime jsRuntime)
    {
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
        _formatter = new() { StringLengthFunc = WideCharStringLength };

        // Periodically check for settings changes and send them to local storage if found.
        _timer = new(CheckSettingsBackup, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    public void DoFormat()
    {
        try
        {
            var input = (ViewOptions.ViewMode==ViewMode.Unified)? _combinedJson : _inputJson;
            _formatter.Options = Options;
            OutputJson = _formatter.Reformat(input, 0);
            CombinedJson = OutputJson;
            StandaloneErrorMsg = string.Empty;
        }
        catch (FracturedJsonException e)
        {
            OutputJson = e.Message;
            StandaloneErrorMsg = e.Message;

            if (e.InputPosition != null)
                Task.Run(() => SetSelectedText(e.InputPosition.Value.Index, e.InputPosition.Value.Index + 1));
        }
    }

    public void DoMinify()
    {
        try
        {
            var input = (ViewOptions.ViewMode==ViewMode.Unified)? _combinedJson : _inputJson;
            _formatter.Options = Options;
            OutputJson = _formatter.Minify(input);
            CombinedJson = OutputJson;
            StandaloneErrorMsg = string.Empty;
        }
        catch (FracturedJsonException e)
        {
            OutputJson = e.Message;
            StandaloneErrorMsg = e.Message;

            if (e.InputPosition != null)
                Task.Run(() => SetSelectedText(e.InputPosition.Value.Index, e.InputPosition.Value.Index + 1));
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

        var restoredView = await _localStorage.GetItemAsync<ViewOptions>(_viewKey);
        ViewOptions = restoredView ?? new ViewOptions();
        _lastSavedViewOptions = ViewOptions with {};

        await ApplyThemeToDom();
        SomethingHappened?.Invoke();
    }

    public void ShowSettings()
    {
        ViewOptions.ShowSettings = true;
        SaveViewOptions();
    }

    public void HideSettings()
    {
        ViewOptions.ShowSettings = false;
        SaveViewOptions();
    }

    public void SetModeOverUnder()
    {
        SetViewMode(ViewMode.OverUnder);
    }

    public void SetModeSideBySide()
    {
        SetViewMode(ViewMode.SideBySide);
    }

    public void SetModeUnified()
    {
        SetViewMode(ViewMode.Unified);
    }

    public void SetViewMode(ViewMode newMode)
    {
        if (newMode == ViewOptions.ViewMode)
            return;
        if (newMode == ViewMode.Unified)
            _combinedJson = _inputJson;
        if (ViewOptions.ViewMode == ViewMode.Unified)
            _inputJson = _combinedJson;

        ViewOptions.ViewMode = newMode;
        SaveViewOptions();
        SomethingHappened?.Invoke();
    }

    public void ToggleDarkTheme()
    {
        ViewOptions.DarkTheme = !ViewOptions.DarkTheme;
        SaveViewOptions();
        Task.Run(ApplyThemeToDom);
        SomethingHappened?.Invoke();
    }

    public void SetSamplePureJson()
    {
        InputJson = SampleJson.PureJson.ReplaceLineEndings(string.Empty);
        OutputJson = string.Empty;
        CombinedJson = SampleJson.PureJson;
        StandaloneErrorMsg = string.Empty;
    }

    public void SetSampleWithComments()
    {
        InputJson = SampleJson.JsonWithComments;
        OutputJson = string.Empty;
        CombinedJson = SampleJson.JsonWithComments;
        StandaloneErrorMsg = string.Empty;
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
    private const string _viewKey = "view";

    private readonly Formatter _formatter;
    private readonly ILocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;
    private Timer? _timer;
    private FracturedJsonOptions _lastSavedOptions = new();
    private ViewOptions _lastSavedViewOptions = new();
    private string _inputJson = string.Empty;
    private string _outputJson = string.Empty;
    private string _combinedJson = string.Empty;

    private static FracturedJsonOptions GetDefaultOptions()
    {
        return new()
        {
            MaxTotalLineLength = 100,
            CommentPolicy = CommentPolicy.Preserve,
            PreserveBlankLines = true,
            TableCommaPlacement = TableCommaPlacement.BeforePadding,
        };
    }

    /// <summary>
    /// Run on a timer.  Saves the current settings to local storage if they've changed.
    /// </summary>
    private void CheckSettingsBackup(object? state)
    {
        if (Options == _lastSavedOptions)
            return;

        _lastSavedOptions = Options with {};
        Task.Run(() => _localStorage.SetItemAsync(_optionsKey, Options));
    }

    /// <summary>
    /// Save the new view settings to local storage.  (We execute this one whenever they press the button.)
    /// </summary>
    private void SaveViewOptions()
    {
        if (ViewOptions == _lastSavedViewOptions)
            return;

        _lastSavedViewOptions = ViewOptions with {};
        Task.Run(() => _localStorage.SetItemAsync(_viewKey, ViewOptions));
    }

    /// <summary>
    /// Sets an attribute on the root that controls CSS color variables.
    /// </summary>
    private async Task ApplyThemeToDom()
    {
        await _jsRuntime.InvokeVoidAsync("document.documentElement.setAttribute",
            "dark-theme",
            ViewOptions.DarkTheme.ToString().ToLower());
    }

    public async Task SetSelectedText(int startIndex, int endIndex)
    {
        await _jsRuntime.InvokeVoidAsync("setTextareaSelection", "jsonInput", startIndex, endIndex);
        SomethingHappened?.Invoke();
    }

    /// <summary>
    /// Support for East Asian Full Width characters - symbols that take up two monospaced Latin characters' worth
    /// of space.  (Needs an appropriate font.)
    /// </summary>
    private static int WideCharStringLength(string str)
    {
        return str.EnumerateRunes().Sum(rune => UnicodeCalculator.GetWidth(rune.Value));
    }
}
