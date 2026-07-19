using BenchmarkDotNet.Attributes;
using FracturedJson;

namespace Benchmarks;

/// <summary>
/// Benchmarks <see cref="Formatter.Reformat(IEnumerable{char}, int)"/> across a variety of real-world
/// input sizes and shapes. Sample files live under <c>Data/</c>; see <c>Data/DATA-SOURCES.md</c>.
/// </summary>
[MemoryDiagnoser]
public class ReformatBenchmarks
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

    private string _jsonText = null!;
    private Formatter _formatter = null!;

    [GlobalSetup]
    public void Setup()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", InputFile);
        _jsonText = File.ReadAllText(path);

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

    [Benchmark]
    public string Reformat() => _formatter.Reformat(_jsonText, 0);
}
