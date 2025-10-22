using System.Globalization;
using FracturedJson;

namespace Tests;

[TestClass]
public class BufferEquivalenceTests
{
    /// <summary>
    /// Generates combos of input JSON and Formatter options to feed to all the tests.
    /// </summary>
    public static IEnumerable<object[]> GenerateUniversalParams()
    {
        var testFilesDir = new DirectoryInfo("StandardJsonFiles");
        foreach (var file in testFilesDir.EnumerateFiles("*.json"))
        {
            var fileData = File.ReadAllText(file.FullName);
            foreach (var options in GenerateOptions())
                yield return [fileData, options];
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
                yield return [fileData, moddedOpts];
            }
        }
    }

    /// <summary>
    /// Generates formatter options with a few sets of property settings.
    /// </summary>
    private static IEnumerable<FracturedJsonOptions> GenerateOptions()
    {
        yield return new();
        yield return new() { JsonEolStyle = EolStyle.Lf };
        yield return new()
        {
            NestedBracketPadding = false,
            SimpleBracketPadding = true,
            ColonPadding = false,
            CommaPadding = false,
            IndentSpaces = 3,
            PrefixString = "\t\t",
            JsonEolStyle = EolStyle.Crlf,
        };
    }


    /// <summary>
    /// Tests that both overloads of Reformat produce the same output.
    /// </summary>
    [DataTestMethod]
    [DynamicData(nameof(GenerateUniversalParams), DynamicDataSourceType.Method)]
    public void ReformatSameForBothOverrides(string inputText, FracturedJsonOptions options)
    {
        var formatter = new Formatter() { Options = options };

        // Format directly as a string.
        var formattedAsString = formatter.Reformat(inputText, 0);

        // Format to a stream Writer.
        using var memWriter = new StringWriter();
        formatter.Reformat(inputText, 0, memWriter);
        var writerString = memWriter.ToString();

        Assert.AreEqual(writerString, formattedAsString);
    }


    /// <summary>
    /// Tests that both overloads of Serialize produce the same output.
    /// </summary>
    [TestMethod]
    public void SerializeSameForBothOverrides()
    {
        var formatter = new Formatter();
        var input = CultureInfo.InvariantCulture.NumberFormat;

        var formattedAsString = formatter.Serialize(input, 0);

        using var memWriter = new StringWriter();
        formatter.Serialize(input, 0, memWriter);
        var writerString = memWriter.ToString();

        Assert.AreEqual(writerString, formattedAsString);
    }
}

