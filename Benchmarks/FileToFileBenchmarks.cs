using BenchmarkDotNet.Attributes;
using FracturedJson;

namespace Benchmarks;

/// <summary>
/// Benchmarks testing Formatter.Reformat using files for both input and output.
/// </summary>
[MemoryDiagnoser]
public class FileToFileBenchmarks
{
    /// <summary>
    /// Relative path under the copied <c>Data/</c> directory.
    /// </summary>
    [ParamsSource(nameof(InputFiles))]
    public string InputFile { get; set; } = null!;

    public static IEnumerable<string> InputFiles { get; } =
    [
        "battleplan.json",
        "fjjs-tsconfig.jsonc",
        "geojson-lg.json",
        "geojson-sm.json",
        "pokeapi-lg.json",
        "pokeapi-md.json",
        "pokeapi-sm.json",
    ];

    private string _inputFilePath = string.Empty;
    private string _outputFilePath = string.Empty;
    private Formatter _formatter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _inputFilePath = Path.Combine(AppContext.BaseDirectory, "Data", InputFile);
        _outputFilePath = Path.GetFileNameWithoutExtension(_inputFilePath) + ".out";

        // JSONC (e.g. tsconfig) commonly allows comments and trailing commas.
        var options = Path.GetExtension(InputFile).Equals(".jsonc", StringComparison.OrdinalIgnoreCase)
            ? FracturedJsonOptions.Recommended() with
            {
                CommentPolicy = CommentPolicy.Preserve,
                AllowTrailingCommas = true,
            }
            : FracturedJsonOptions.Recommended();

        _formatter = new Formatter { Options = options };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_outputFilePath))
            File.Delete(_outputFilePath);
    }

    [Benchmark]
    public void FileToFileWithWriter()
    {
        var fileData = File.ReadAllText(_inputFilePath);
        using var writer = new StreamWriter(_outputFilePath);
        _formatter.Reformat(fileData, 0, writer);
    }

    [Benchmark]
    public void FileToFileAsString()
    {
        var fileData = File.ReadAllText(_inputFilePath);
        var formattedJson = _formatter.Reformat(fileData, 0);
        File.WriteAllText(_outputFilePath, formattedJson);
    }
}