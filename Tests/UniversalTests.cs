using System.Text.Json;
using FracturedJson;

namespace Tests;

/// <summary>
/// Tests that should pass with ANY input and ANY settings, within a few constraints:
/// <list type="bullet">
///     <item>The input is valid JSON</item>
///     <item>Input strings may not contain any of []{}:,\n</item>
///     <item>Values given to <see cref="FracturedJson.Formatter.PrefixString"/> may only contain whitespace.</item>
/// </list>
/// 
/// Those rules exist to make the output easy to test without understanding the grammar.  Other files might contain
/// tests that don't impose these restrictions.
/// </summary>
[TestClass]
public class UniversalTests
{
    /// <summary>
    /// Tests that the output is actually valid JSON.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void IsWellFormed(string inputText, Formatter formatter)
    {
        var outputText = formatter.Serialize(inputText);
        
        // Parse will throw an exception if its input (Formatter output) isn't well-formed.
        var _ = JsonDocument.Parse(outputText);
    }

    /// <summary>
    /// Any string that exists in the input should exist somewhere in the output.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void AllStringsExist(string inputText, Formatter formatter)
    {
        var outputText = formatter.Serialize(inputText);
        
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
    public void MaxLengthRespected(string inputText, Formatter formatter)
    {
        const string structuralChars = "[]{}:,";
        
        var outputText = formatter.Serialize(inputText);
        var outputLines = outputText.Split(EolString(formatter));
        
        foreach (var line in outputLines)
        {
            var (_, content) = LineContent(formatter, line);
            
            // If the content is shorter than the max, it's all good.
            if (content.Length <= formatter.MaxInlineLength)
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
    
    /// <summary>
    /// Makes sure - as best we can - that the properties <see cref="FracturedJson.Formatter.MaxInlineComplexity"/> and
    /// <see cref="FracturedJson.Formatter.MaxCompactArrayComplexity"/> are honored.  
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void MaxInlineComplexityRespected(string inputText, Formatter formatter)
    {
        var outputText = formatter.Serialize(inputText);
        var outputLines = outputText.Split(EolString(formatter));

        // Look at each line of the output separately, counting the nesting level in each.  
        foreach (var line in outputLines)
        {
            var (_, content) = LineContent(formatter, line);
            
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
                Assert.IsTrue(nestLevel <= formatter.MaxCompactArrayComplexity);
                return;
            }
            
            // Otherwise, we can't actually tell if it's a compact array or inline by looking at just the one line.
            Assert.IsTrue(nestLevel <= formatter.MaxInlineComplexity
                          || nestLevel <= formatter.MaxCompactArrayComplexity);
        }
    }

    /// <summary>
    /// Generates combos of input JSON and Formatter property values to feed to all of the tests.
    /// </summary>
    public static IEnumerable<object[]> GenerateUniversalParams()
    {
        var testFilesDir = new DirectoryInfo("StandardJsonFiles");
        foreach (var file in testFilesDir.EnumerateFiles("*.json"))
        {
            var fileData = File.ReadAllText(file.FullName);
            foreach (var formatter in GenerateFormatters())
                yield return new object[] { fileData, formatter };
        }
    }

    /// <summary>
    /// Generates Formatters with a wide variety of property settings.  We can't test every permutation of settings,
    /// but hopefully we can provide enough to make it hard for bugs to stay hidden.
    /// </summary>
    private static IEnumerable<Formatter> GenerateFormatters()
    {
        yield return new Formatter();
        yield return new Formatter() { MaxInlineComplexity = 10000 };
        yield return new Formatter() { MaxCompactArrayComplexity = 2 };
        yield return new Formatter() { MaxCompactArrayComplexity = -1 };
        yield return new Formatter() { MaxInlineLength = int.MaxValue };
        yield return new Formatter() { MaxInlineLength = 20 };
        yield return new Formatter() { MaxInlineComplexity = 3, MaxInlineLength = 150, JsonEolStyle = EolStyle.Crlf };
        yield return new Formatter() { MaxInlineComplexity = 3, MaxInlineLength = 150, JsonEolStyle = EolStyle.Lf };
        yield return new Formatter() { MaxInlineComplexity = 0, MaxCompactArrayComplexity = 2, MaxInlineLength = 150 };
        yield return new Formatter()
        {
            NestedBracketPadding = false,
            SimpleBracketPadding = true,
            ColonPadding = false,
            CommaPadding = false,
            IndentSpaces = 3,
            PrefixString = "\t\t"
        };
        yield return new Formatter()
            { MaxInlineLength = 120, TableArrayMinimumSimilarity = 20, TableObjectMinimumSimilarity = 20 };
        yield return new Formatter()
            { MaxInlineLength = 120, TableArrayMinimumSimilarity = 101, TableObjectMinimumSimilarity = 101 };
        yield return new Formatter()
            { 
                MaxInlineLength = int.MaxValue, 
                MaxInlineComplexity = int.MaxValue, 
                AlwaysExpandDepth = 1,
                AlignExpandedPropertyNames = true,
            };
        yield return new Formatter()
            { UseTabToIndent = true, DontJustifyNumbers = true, StringWidthFunc = Formatter.StringWidthWithEastAsian };
    }

    private static string EolString(Formatter formatter)
    {
        return formatter.JsonEolStyle switch
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
    private static (string,string) LineContent(Formatter formatter, string line)
    {
        // Skip past the prefix string and whitespace.
        if (!line.StartsWith(formatter.PrefixString))
            throw new Exception("Output line does not begin with prefix string");
        var lineAfterPrefix = line.Substring(formatter.PrefixString.Length).TrimStart();
        
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
}
