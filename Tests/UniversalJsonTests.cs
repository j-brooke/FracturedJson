using System.Text.Json;
using FracturedJson;

namespace Tests;

/// <summary>
/// <para>Tests that should pass with ANY input and ANY settings, within a few constraints.  These aren't particularly
/// focused tests.  The idea is to throw a variety of inputs with a wide variety of settings and make sure
/// nothing horribly unexpected happens.</para>
///
/// <para>Constraints:</para>
/// <list type="bullet">
///     <item>The input is valid JSON</item>
///     <item>Input strings may not contain any of []{}:,\n</item>
///     <item>Values given to PrefixString" may only contain whitespace.</item>
/// </list>
///
/// <para>Those rules exist to make the output easy to test without understanding the grammar.  Other files might contain
/// tests that don't impose these restrictions.</para>
/// </summary>
[TestClass]
public class UniversalJsonTests
{
    /// <summary>
    /// Generates combos of input JSON and Formatter options to feed to all of the tests.
    /// </summary>
    public static IEnumerable<object[]> GenerateUniversalParams()
    {
        var testFilesDir = new DirectoryInfo("StandardJsonFiles");
        foreach (var file in testFilesDir.EnumerateFiles("*.json"))
        {
            var fileData = File.ReadAllText(file.FullName);
            foreach (var options in GenerateOptions())
                yield return new object[] { fileData, options };
        }

        var commentTestFilesDir = new DirectoryInfo("FilesWithComments");
        foreach (var file in commentTestFilesDir.EnumerateFiles("*.jsonc"))
        {
            var fileData = File.ReadAllText(file.FullName);
            foreach (var options in GenerateOptions())
            {
                var moddedOpts = options with
                {
                    CommentPolicy = CommentPolicy.Preserve,
                    PreserveBlankLines = true,
                };
                yield return new object[] { fileData, moddedOpts };
            }
        }
    }

    /// <summary>
    /// Generates formatter options with a wide variety of property settings.  We can't test every permutation of
    /// settings, but hopefully we can provide enough to make it hard for bugs to stay hidden.
    /// </summary>
    private static IEnumerable<FracturedJsonOptions> GenerateOptions()
    {
        yield return new();
        yield return new() { MaxInlineComplexity = 10000 };
        yield return new() { MaxInlineLength = int.MaxValue };
        yield return new() { MaxInlineLength = 23 };
        yield return new() { MaxInlineLength = 59 };
        yield return new() { MaxTotalLineLength = 59 };
        yield return new() { JsonEolStyle = EolStyle.Crlf };
        yield return new() { JsonEolStyle = EolStyle.Lf };
        yield return new() { JsonEolStyle = EolStyle.Default };
        yield return new() { MaxInlineComplexity = 0, MaxCompactArrayComplexity = 0, MaxTableRowComplexity = 0 };
        yield return new() { MaxInlineComplexity = 2, MaxCompactArrayComplexity = 0, MaxTableRowComplexity = 0 };
        yield return new() { MaxInlineComplexity = 0, MaxCompactArrayComplexity = 2, MaxTableRowComplexity = 0 };
        yield return new() { MaxInlineComplexity = 0, MaxCompactArrayComplexity = 0, MaxTableRowComplexity = 2 };
        yield return new()
        {
            NestedBracketPadding = false,
            SimpleBracketPadding = true,
            ColonPadding = false,
            CommaPadding = false,
            IndentSpaces = 3,
            PrefixString = "\t\t"
        };
    }

    private static string EolString(FracturedJsonOptions options)
    {
        return options.JsonEolStyle switch
        {
            EolStyle.Crlf => "\r\n",
            EolStyle.Lf => "\n",
            EolStyle.Default => Environment.NewLine,
            _ => Environment.NewLine
        };
    }

    private static string SkipPrefixAndIndent(FracturedJsonOptions options, string line)
    {
        // Skip past the prefix string and whitespace.
        if (!line.StartsWith(options.PrefixString))
            throw new Exception("Output line does not begin with prefix string");
        var lineAfterPrefix = line.Substring(options.PrefixString.Length).TrimStart();
        return lineAfterPrefix;
    }

    /// <summary>
    /// Tests that the output is actually valid JSON.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void IsWellFormed(string inputText, FracturedJsonOptions options)
    {
        var formatter = new Formatter() { Options = options };
        var outputText = formatter.Reformat(inputText, 0);

        // Parse will throw an exception if its input (Formatter output) isn't well-formed.
        var sysOpts = new JsonDocumentOptions();
        if (options.CommentPolicy != CommentPolicy.TreatAsError)
            sysOpts.CommentHandling = JsonCommentHandling.Skip;
        var _ = JsonDocument.Parse(outputText, sysOpts);
    }


    /// <summary>
    /// Any string that exists in the input should exist somewhere in the output.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void AllStringsExist(string inputText, FracturedJsonOptions options)
    {
        var formatter = new Formatter() { Options = options };
        var outputText = formatter.Reformat(inputText, 0);

        var startPos = 0;
        while (true)
        {
            while (startPos < inputText.Length && inputText[startPos] != '"')
                startPos += 1;

            var endPos = startPos + 1;
            while (endPos < inputText.Length && inputText[endPos] != '"')
                endPos += 1;

            if (endPos >= inputText.Length)
                return;

            var stringFromSource = inputText.Substring(startPos+1, endPos - startPos - 2);
            StringAssert.Contains(outputText, stringFromSource);
            startPos = endPos + 1;
        }
    }


    /// <summary>
    /// Makes sure that the length restriction properties are respected.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void MaxLengthRespected(string inputText, FracturedJsonOptions options)
    {
        var formatter = new Formatter() { Options = options };
        var outputText = formatter.Reformat(inputText, 0);
        var outputLines = outputText.TrimEnd().Split(EolString(options));

        foreach (var line in outputLines)
        {
            var content = SkipPrefixAndIndent(options, line);

            // If the content is shorter than the max, it's all good.
            if (content.Length <= options.MaxInlineLength && line.Length <= options.MaxTotalLineLength)
                continue;

            // We'll consider it a single element if there's no more than one comma.
            var commaCount = content.Count(ch => ch == ',');
            Assert.IsTrue(commaCount <= 1);
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void MaxInlineComplexityRespected(string inputText, FracturedJsonOptions options)
    {
        var formatter = new Formatter() { Options = options };
        var outputText = formatter.Reformat(inputText, 0);
        var outputLines = outputText.TrimEnd().Split(EolString(options));

        // Look at each line of the output separately, counting the nesting level in each.
        foreach (var line in outputLines)
        {
            var content = SkipPrefixAndIndent(options, line);

            // Keep a running total of opens vs closes.  Since Formatter treats empty arrays and objects as complexity
            // zero just like primitives, we don't update nestLevel until we see something other than an empty.
            var openCount = 0;
            var nestLevel = 0;
            var topLevelCommaSeen = false;
            var multipleTopLevelItems = false;
            foreach (var ch in content)
            {
                switch (ch)
                {
                    case ' ':
                    case '\t':
                        break;
                    case '[':
                    case '{':
                        multipleTopLevelItems |= (topLevelCommaSeen && openCount == 0);
                        openCount += 1;
                        break;
                    case ']':
                    case '}':
                        openCount -= 1;
                        nestLevel = Math.Max(nestLevel, openCount);
                        break;
                    default:
                        multipleTopLevelItems |= (topLevelCommaSeen && openCount == 0);
                        if (ch == ',')
                            topLevelCommaSeen |= (openCount == 0);
                        nestLevel = Math.Max(nestLevel, openCount);
                        break;
                }
            }

            // If there were multiple top-level items on the line, this must be a compact array case.  Comments mess
            // with the "top level" heuristic though.
            if (multipleTopLevelItems && options.CommentPolicy != CommentPolicy.Preserve)
            {
                Assert.IsTrue(nestLevel <= options.MaxCompactArrayComplexity);
                return;
            }

            // Otherwise, we can't actually tell if it's a compact array, table, or inline by looking at just the one line.
            Assert.IsTrue(nestLevel <= options.MaxInlineComplexity
                          || nestLevel <= options.MaxCompactArrayComplexity
                          || nestLevel <= options.MaxTableRowComplexity);
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void RepeatedFormattingIsStable(string inputText, FracturedJsonOptions options)
    {
        var mainFormatter = new Formatter() { Options = options };
        var initialOutput = mainFormatter.Reformat(inputText, 0);

        var crunchOutput = mainFormatter.Minify(initialOutput);
        var backToStartOutput1 = mainFormatter.Reformat(crunchOutput, 0);

        // We formatted it, then minified that, then reformatted that.  It should be the same.
        Assert.AreEqual(initialOutput, backToStartOutput1);

        var expandOptions = new FracturedJsonOptions()
        {
            AlwaysExpandDepth = int.MaxValue,
            CommentPolicy = CommentPolicy.Preserve,
            PreserveBlankLines = true,
        };
        var expandFormatter = new Formatter() { Options = expandOptions };
        var expandOutput = expandFormatter.Reformat(crunchOutput, 0);

        var backToStartOutput2 = mainFormatter.Reformat(expandOutput, 0);

        // For good measure, we took the minified output and expanded it as much as possible, and then formatted that.
        // Again, it should be the same as our original formatting.
        Assert.AreEqual(initialOutput, backToStartOutput2);
    }
}
