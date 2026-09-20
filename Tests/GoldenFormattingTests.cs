using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FracturedJson;

namespace Tests;

/// <summary>
/// Exact-output snapshots of representative Formatter cases, driven by the language-neutral files under
/// <c>Golden/</c> (see that folder's README). Existing unit tests check validity, commas, and alignment; these
/// lock the full character grid so a Formatter cleanup cannot drift silently.
/// <para>
/// All cases force LF line endings so the files are stable across machines.
/// </para>
/// <para>
/// If a refactor changes output intentionally, regenerate with
/// <c>FRACTUREDJSON_UPDATE_GOLDENS=1</c> and inspect the diffs.  If the change was not intentional, do not
/// update the files — fix the code. Regeneration writes expected <c>.txt</c> files only, not the manifest.
/// </para>
/// </summary>
[TestClass]
[TestCategory("Golden")]
public class GoldenFormattingTests
{
    [DataTestMethod]
    [DynamicData(nameof(CaseIds), DynamicDataSourceType.Method)]
    public void OutputMatchesGolden(string caseId)
    {
        var suite = _suite.Value;
        var golden = suite.Manifest.Cases.First(c => c.Id == caseId);
        var options = BuildOptions(suite, golden);

        var inputPath = Path.Combine(OutputGoldenRoot(), golden.Input);
        Assert.IsTrue(File.Exists(inputPath), $"Missing golden input: {inputPath}");
        var input = File.ReadAllText(inputPath);

        var formatter = new Formatter { Options = options };
        var startingDepth = golden.StartingDepth ?? 0;
        var actual = IsMinify(golden)
            ? formatter.Minify(input)
            : formatter.Reformat(input, startingDepth);

        var outputExpectedPath = Path.Combine(OutputGoldenRoot(), golden.Expected);
        var update = Environment.GetEnvironmentVariable("FRACTUREDJSON_UPDATE_GOLDENS") == "1";

        if (update)
        {
            var sourcePath = Path.Combine(FindSourceGoldenRoot(), golden.Expected);
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, actual, _utf8NoBom);
            Directory.CreateDirectory(Path.GetDirectoryName(outputExpectedPath)!);
            File.WriteAllText(outputExpectedPath, actual, _utf8NoBom);
        }

        if (!File.Exists(outputExpectedPath))
        {
            Assert.Fail(
                $"Missing golden expected file for {caseId}: {outputExpectedPath}. " +
                "Re-run with FRACTUREDJSON_UPDATE_GOLDENS=1 to create it.");
        }

        var expected = File.ReadAllText(outputExpectedPath);
        if (expected == actual)
            return;

        var actualDumpPath = Path.Combine(AppContext.BaseDirectory, "GoldenActual", golden.Expected);
        Directory.CreateDirectory(Path.GetDirectoryName(actualDumpPath)!);
        File.WriteAllText(actualDumpPath, actual, _utf8NoBom);

        Assert.Fail(DescribeMismatch(caseId, expected, actual, actualDumpPath));
    }

    [TestMethod]
    public void DefaultsFileMatchesConstructor()
    {
        var json = File.ReadAllText(Path.Combine(OutputGoldenRoot(), "defaults-v5-constructor.json"));
        using var doc = JsonDocument.Parse(json);
        var skip = _cSharpOnlyOptionNames;

        foreach (var name in skip)
        {
            Assert.IsFalse(doc.RootElement.TryGetProperty(name, out _),
                $"{name} is C#-only / unreleased in 5.0 and should not be in the v1 defaults file");
        }

        foreach (var prop in typeof(FracturedJsonOptions).GetProperties())
        {
            if (skip.Contains(prop.Name) || !prop.CanWrite)
                continue;
            Assert.IsTrue(doc.RootElement.TryGetProperty(prop.Name, out _),
                $"defaults-v5-constructor.json is missing {prop.Name}");
        }

        var ctor = new FracturedJsonOptions { JsonEolStyle = EolStyle.Lf };
        var fromFile = JsonSerializer.Deserialize<FracturedJsonOptions>(json, _optionsJson)!;
        foreach (var prop in typeof(FracturedJsonOptions).GetProperties())
        {
            if (skip.Contains(prop.Name) || !prop.CanWrite)
                continue;
            Assert.AreEqual(prop.GetValue(ctor), prop.GetValue(fromFile), prop.Name);
        }
    }

    public static IEnumerable<object[]> CaseIds()
    {
        foreach (var golden in _suite.Value.Manifest.Cases)
            yield return [golden.Id];
    }


    private static readonly Encoding _utf8NoBom = new UTF8Encoding(false);
    private static readonly Lazy<LoadedSuite> _suite = new(LoadSuite);

    /// <summary>
    /// Options present on C# main that are not part of the published 5.0 JS option set. They default to off and
    /// must not appear in the v1 defaults file.
    /// </summary>
    private static readonly HashSet<string> _cSharpOnlyOptionNames =
    [
        nameof(FracturedJsonOptions.AllowTableSegments),
        nameof(FracturedJsonOptions.SplitTableSegmentsAtBlankLines),
        nameof(FracturedJsonOptions.SplitTableSegmentsAtComments),
        nameof(FracturedJsonOptions.CollapseOpeningBrackets),
        nameof(FracturedJsonOptions.CollapseClosingBrackets),
    ];

    private static readonly JsonSerializerOptions _manifestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly JsonSerializerOptions _optionsJson = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    private static LoadedSuite LoadSuite()
    {
        var root = OutputGoldenRoot();
        var manifestPath = Path.Combine(root, "manifest.json");
        Assert.IsTrue(File.Exists(manifestPath), $"Missing golden manifest: {manifestPath}");

        var manifest = JsonSerializer.Deserialize<ManifestFile>(File.ReadAllText(manifestPath), _manifestJson)
                       ?? throw new InvalidOperationException("Golden manifest deserialized to null");

        Assert.AreEqual(1, manifest.SuiteVersion, "Unsupported golden suiteVersion");
        Assert.IsFalse(string.IsNullOrEmpty(manifest.DefaultsFile), "manifest defaultsFile is required");

        var defaultsPath = Path.Combine(root, manifest.DefaultsFile);
        Assert.IsTrue(File.Exists(defaultsPath), $"Missing defaults file: {defaultsPath}");
        var defaults = JsonNode.Parse(File.ReadAllText(defaultsPath)) as JsonObject
                       ?? throw new InvalidOperationException("Defaults file must be a JSON object");

        Assert.IsTrue(manifest.Cases.Count > 0, "Golden manifest has no cases");
        var duplicateIds = manifest.Cases.GroupBy(c => c.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.IsFalse(duplicateIds.Length > 0, "Duplicate golden case ids: " + string.Join(", ", duplicateIds));

        foreach (var golden in manifest.Cases)
        {
            Assert.IsFalse(string.IsNullOrEmpty(golden.Id), "Golden case is missing id");
            Assert.IsFalse(string.IsNullOrEmpty(golden.Input), $"{golden.Id}: missing input");
            Assert.IsFalse(string.IsNullOrEmpty(golden.Expected), $"{golden.Id}: missing expected");
            var op = golden.Operation ?? "reformat";
            Assert.IsTrue(op is "reformat" or "minify", $"{golden.Id}: operation must be reformat or minify");
        }

        return new LoadedSuite(manifest, defaults);
    }

    private static FracturedJsonOptions BuildOptions(LoadedSuite suite, ManifestCase golden)
    {
        var merged = (JsonObject)suite.Defaults.DeepClone();
        ApplyPatch(merged, golden.Options, golden.Id);
        ApplyPatch(merged, suite.Manifest.Forced, golden.Id);

        try
        {
            return merged.Deserialize<FracturedJsonOptions>(_optionsJson)
                   ?? throw new InvalidOperationException($"{golden.Id}: options deserialized to null");
        }
        catch (JsonException ex)
        {
            Assert.Fail($"{golden.Id}: invalid options ({ex.Message})");
            return null!;
        }
    }

    private static void ApplyPatch(JsonObject target, JsonObject? patch, string caseId)
    {
        if (patch == null)
            return;

        foreach (var kv in patch)
        {
            try
            {
                target[kv.Key] = kv.Value?.DeepClone();
            }
            catch (Exception ex)
            {
                Assert.Fail($"{caseId}: failed to apply option '{kv.Key}' ({ex.Message})");
            }
        }
    }

    private static bool IsMinify(ManifestCase golden) =>
        string.Equals(golden.Operation, "minify", StringComparison.Ordinal);

    private static string OutputGoldenRoot() => Path.Combine(AppContext.BaseDirectory, "Golden");

    private static string FindSourceGoldenRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var golden = Path.Combine(dir.FullName, "Golden");
            if (File.Exists(Path.Combine(dir.FullName, "Tests.csproj")) && Directory.Exists(golden))
                return golden;
        }

        Assert.Fail(
            "Could not find Tests/Golden in the source tree. " +
            "FRACTUREDJSON_UPDATE_GOLDENS only works when tests run from this repo.");
        return "";
    }

    private static string DescribeMismatch(string caseId, string expected, string actual, string actualDumpPath)
    {
        var expLines = SplitKeepEmpty(expected);
        var actLines = SplitKeepEmpty(actual);
        var lineCount = Math.Max(expLines.Length, actLines.Length);
        var firstDiff = -1;
        for (var i = 0; i < lineCount; ++i)
        {
            var expLine = i < expLines.Length ? expLines[i] : "<missing line>";
            var actLine = i < actLines.Length ? actLines[i] : "<missing line>";
            if (expLine != actLine)
            {
                firstDiff = i;
                break;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Golden output changed for {caseId}.");
        sb.AppendLine($"Actual written to {actualDumpPath}");
        sb.AppendLine("If this was intentional, re-run with FRACTUREDJSON_UPDATE_GOLDENS=1 and inspect the diff.");
        if (firstDiff >= 0)
        {
            sb.AppendLine($"First difference at line {firstDiff + 1}:");
            sb.AppendLine($"  expected: {ShowLine(firstDiff, expLines)}");
            sb.AppendLine($"  actual:   {ShowLine(firstDiff, actLines)}");
        }

        return sb.ToString();
    }

    private static string[] SplitKeepEmpty(string text) => text.Split('\n');

    private static string ShowLine(int index, string[] lines)
    {
        if (index >= lines.Length)
            return "<missing line>";
        return lines[index].Replace(' ', '·');
    }

    private sealed record LoadedSuite(ManifestFile Manifest, JsonObject Defaults);

    private sealed class ManifestFile
    {
        public int SuiteVersion { get; set; }
        public string FamilyVersion { get; set; } = "";
        public string? GeneratedFrom { get; set; }
        public string? DefaultsProfile { get; set; }
        public string DefaultsFile { get; set; } = "";
        public JsonObject? Forced { get; set; }
        public List<ManifestCase> Cases { get; set; } = [];
    }

    private sealed class ManifestCase
    {
        public string Id { get; set; } = "";
        public string Input { get; set; } = "";
        public string Expected { get; set; } = "";
        public string? Operation { get; set; }
        public int? StartingDepth { get; set; }
        public JsonObject? Options { get; set; }
    }
}
