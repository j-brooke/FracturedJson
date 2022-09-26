using System.Text.Json;
using FracturedJson.V3;

namespace Tests;

[TestClass]
public class V3UniversalFormatterTests
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
            foreach (var formatter in GenerateOptions())
                yield return new object[] { fileData, formatter };
        }
    }

    /// <summary>
    /// Generates Formatters with a wide variety of property settings.  We can't test every permutation of settings,
    /// but hopefully we can provide enough to make it hard for bugs to stay hidden.
    /// </summary>
    private static IEnumerable<FracturedJsonOptions> GenerateOptions()
    {
        yield return new FracturedJsonOptions();
        yield return new FracturedJsonOptions() { MaxInlineComplexity = 10000 };
        yield return new FracturedJsonOptions() { MaxCompactArrayComplexity = 2 };
        yield return new FracturedJsonOptions() { MaxCompactArrayComplexity = -1 };
        yield return new FracturedJsonOptions() { MaxInlineLength = int.MaxValue };
        yield return new FracturedJsonOptions() { MaxInlineLength = 20 };
        yield return new FracturedJsonOptions() { MaxInlineComplexity = 3, MaxInlineLength = 150, JsonEolStyle = EolStyle.Crlf };
        yield return new FracturedJsonOptions() { MaxInlineComplexity = 3, MaxInlineLength = 150, JsonEolStyle = EolStyle.Lf };
        yield return new FracturedJsonOptions() { MaxInlineComplexity = 0, MaxCompactArrayComplexity = 2, MaxInlineLength = 150 };
        yield return new FracturedJsonOptions()
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

    /// <summary>
    /// Returns the object key (if any) and the content of a single line.  Content here means skipping over
    /// any PrefixStrings, indentation, and the key if this is an object KVP.
    /// </summary>
    private static (string,string) LineContent(FracturedJsonOptions options, string line)
    {
        // Skip past the prefix string and whitespace.
        if (!line.StartsWith(options.PrefixString))
            throw new Exception("Output line does not begin with prefix string");
        var lineAfterPrefix = line.Substring(options.PrefixString.Length).TrimStart();

        if (lineAfterPrefix.Length==0)
            return (string.Empty, string.Empty);

        // If the first character is anything other than a quote, the line isn't an object key/value pair,
        // so it's just content.
        if (lineAfterPrefix[0] != '"')
            return (string.Empty, lineAfterPrefix);

        // If the first character was a quote, look for a colon.  For these tests we require that strings
        // never have colons in them, so it must be structural, and it must be after the starting string,
        // if it exists.
        var firstColonPos = lineAfterPrefix.IndexOf(':');
        if (firstColonPos < 0)
            return (string.Empty, lineAfterPrefix);

        var tag = lineAfterPrefix.Substring(0, firstColonPos);
        var content = lineAfterPrefix.Substring(firstColonPos + 1).TrimStart();
        return (tag, content);
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
        var _ = JsonDocument.Parse(outputText);
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
    /// Makes sure that the <see cref="FracturedJson.Formatter.MaxInlineLength"/> property is respected.  Note that
    /// that property refers only to the content of the line, not indentation or object keys.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void MaxLengthRespected(string inputText, FracturedJsonOptions options)
    {
        const string structuralChars = "[]{}:,";

        var formatter = new Formatter() { Options = options };
        var outputText = formatter.Reformat(inputText, 0);
        var outputLines = outputText.TrimEnd().Split(EolString(options));

        foreach (var line in outputLines)
        {
            var (_, content) = LineContent(options, line);

            // If the content is shorter than the max, it's all good.
            if (content.Length <= options.MaxInlineLength)
                continue;

            // If the content is a single element - a long string or number - it's allowed to exceed the limit.
            // We'll consider it a single element if there are no brackets or colons, and no commas other than maybe one
            // at the end.
            var noTrailingComma = content.TrimEnd();
            if (noTrailingComma.EndsWith(','))
                noTrailingComma = noTrailingComma.Substring(0, noTrailingComma.Length - 1);
            var isSingleElem = !noTrailingComma.Any(c => structuralChars.Contains(c));
            Assert.IsTrue(isSingleElem);
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
            var (_, content) = LineContent(options, line);

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

            // If there were multiple top-level items on the line, this must be a compact array case.
            if (multipleTopLevelItems)
            {
                Assert.IsTrue(nestLevel <= options.MaxCompactArrayComplexity);
                return;
            }

            // Otherwise, we can't actually tell if it's a compact array or inline by looking at just the one line.
            Assert.IsTrue(nestLevel <= options.MaxInlineComplexity
                          || nestLevel <= options.MaxCompactArrayComplexity);
        }
    }
}