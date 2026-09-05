using System.Text;
using FracturedJson;

namespace Tests;

/// <summary>
/// Exact-output snapshots of representative Formatter cases.  Existing unit tests check validity, commas, and
/// alignment; these lock the full character grid so a Formatter cleanup cannot drift silently.
/// <para>
/// Each case is a folder under <c>Golden/</c> with an input file and one <c>.txt</c> expected file per variant.
/// All cases force LF line endings so the files are stable across machines.
/// </para>
/// <para>
/// If a refactor changes output intentionally, regenerate with
/// <c>FRACTUREDJSON_UPDATE_GOLDENS=1</c> and inspect the diffs.  If the change was not intentional, do not
/// update the files — fix the code.
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
        var golden = AllCases().First(c => c.Id == caseId);
        var options = golden.Options with { JsonEolStyle = EolStyle.Lf };

        var inputPath = Path.Combine(OutputGoldenRoot(), golden.Folder, golden.InputFile);
        Assert.IsTrue(File.Exists(inputPath), $"Missing golden input: {inputPath}");
        var input = File.ReadAllText(inputPath);

        var formatter = new Formatter { Options = options };
        var actual = golden.Minify ? formatter.Minify(input) : formatter.Reformat(input, 0);

        var expectedFileName = golden.Variant + ".txt";
        var outputExpectedPath = Path.Combine(OutputGoldenRoot(), golden.Folder, expectedFileName);
        var update = Environment.GetEnvironmentVariable("FRACTUREDJSON_UPDATE_GOLDENS") == "1";

        if (update)
        {
            var sourceDir = Path.Combine(FindSourceGoldenRoot(), golden.Folder);
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, expectedFileName), actual, Utf8NoBom);
            Directory.CreateDirectory(Path.GetDirectoryName(outputExpectedPath)!);
            File.WriteAllText(outputExpectedPath, actual, Utf8NoBom);
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

        var actualDumpPath = Path.Combine(AppContext.BaseDirectory, "GoldenActual", golden.Folder, expectedFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(actualDumpPath)!);
        File.WriteAllText(actualDumpPath, actual, Utf8NoBom);

        Assert.Fail(DescribeMismatch(caseId, expected, actual, actualDumpPath));
    }

    public static IEnumerable<object[]> CaseIds()
    {
        foreach (var golden in AllCases())
            yield return [golden.Id];
    }

    /// <summary>
    /// Named snapshots covering Formatter paths that are easy to perturb: compact arrays, nested tables,
    /// table comma placement, postfix // comments, property alignment, dummy commas for missing keys,
    /// null/short-array columns, multiline middle comments, blank lines, and minify.
    /// </summary>
    private static IEnumerable<GoldenCase> AllCases()
    {
        yield return new("compact-array.default", "compact-array", "input.json", "default",
            new FracturedJsonOptions { MaxTotalLineLength = 48 });

        yield return new("compact-array.expanded", "compact-array", "input.json", "expanded",
            new FracturedJsonOptions
            {
                MaxTotalLineLength = 48,
                MaxInlineComplexity = -1,
                MaxCompactArrayComplexity = -1,
                MaxTableRowComplexity = -1,
            });

        yield return new("nested-table.default", "nested-table", "input.json", "default",
            new FracturedJsonOptions());

        yield return new("nested-table.short-line", "nested-table", "input.json", "short-line",
            new FracturedJsonOptions { MaxTotalLineLength = 77 });

        yield return new("nested-table.commas-after-padding", "nested-table", "input.json", "commas-after-padding",
            new FracturedJsonOptions { TableCommaPlacement = TableCommaPlacement.AfterPadding });

        yield return new("always-expand.default", "always-expand", "input.json", "default",
            new FracturedJsonOptions());

        yield return new("always-expand.depth-0", "always-expand", "input.json", "depth-0",
            new FracturedJsonOptions { AlwaysExpandDepth = 0 });

        yield return new("table-with-comments.commas-before-padding", "table-with-comments", "input.jsonc",
            "commas-before-padding",
            Jsonc() with
            {
                MaxTotalLineLength = 40,
                NumberListAlignment = NumberListAlignment.Decimal,
                TableCommaPlacement = TableCommaPlacement.BeforePadding,
            });

        yield return new("table-with-comments.commas-after-padding", "table-with-comments", "input.jsonc",
            "commas-after-padding",
            Jsonc() with
            {
                MaxTotalLineLength = 40,
                NumberListAlignment = NumberListAlignment.Decimal,
                TableCommaPlacement = TableCommaPlacement.AfterPadding,
            });

        yield return new("table-with-comments.minify", "table-with-comments", "input.jsonc", "minify",
            Jsonc(), Minify: true);

        yield return new("postfix-line-comments.expanded", "postfix-line-comments", "input.jsonc", "expanded",
            Jsonc() with
            {
                MaxInlineComplexity = 0,
                MaxCompactArrayComplexity = 0,
                MaxTableRowComplexity = 0,
            });

        yield return new("eol-comment-column.default", "eol-comment-column", "input.jsonc", "default",
            Jsonc());

        yield return new("aligned-props.default", "aligned-props", "input.json", "default",
            new FracturedJsonOptions
            {
                MaxPropNamePadding = 15,
                MaxInlineComplexity = -1,
                MaxCompactArrayComplexity = -1,
                MaxTableRowComplexity = -1,
            });

        yield return new("aligned-props.colon-hugs-name", "aligned-props", "input.json", "colon-hugs-name",
            new FracturedJsonOptions
            {
                MaxPropNamePadding = 15,
                ColonBeforePropNamePadding = true,
                MaxInlineComplexity = -1,
                MaxCompactArrayComplexity = -1,
                MaxTableRowComplexity = -1,
            });

        yield return new("expanded-trailing-comments.default", "expanded-trailing-comments", "input.jsonc", "default",
            Jsonc());

        yield return new("ragged-object-table.default", "ragged-object-table", "input.json", "default",
            new FracturedJsonOptions());

        yield return new("numbers-and-nulls.default", "numbers-and-nulls", "input.json", "default",
            new FracturedJsonOptions { NumberListAlignment = NumberListAlignment.Decimal });

        yield return new("numbers-and-nulls.normalize", "numbers-and-nulls", "input.json", "normalize",
            new FracturedJsonOptions { NumberListAlignment = NumberListAlignment.Normalize });

        yield return new("multiline-middle-comment.default", "multiline-middle-comment", "input.jsonc", "default",
            Jsonc());

        yield return new("table-with-blanks.default", "table-with-blanks", "input.jsonc", "default",
            Jsonc() with { PreserveBlankLines = true });
    }

    private static FracturedJsonOptions Jsonc() => new()
    {
        CommentPolicy = CommentPolicy.Preserve,
    };

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

    private static string[] SplitKeepEmpty(string text)
    {
        return text.Split('\n');
    }

    private static string ShowLine(int index, string[] lines)
    {
        if (index >= lines.Length)
            return "<missing line>";
        return lines[index].Replace(' ', '·');
    }

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private sealed record GoldenCase(
        string Id,
        string Folder,
        string InputFile,
        string Variant,
        FracturedJsonOptions Options,
        bool Minify = false);
}
