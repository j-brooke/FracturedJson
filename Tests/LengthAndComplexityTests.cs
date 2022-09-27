using FracturedJson;

namespace Tests;

/// <summary>
/// Unit tests for complexity and length settings
/// </summary>
[TestClass]
public class LengthAndComplexityTests
{
    [DataTestMethod]
    [DataRow(4, 1)] // All on one line
    [DataRow(3, 3)] // Outer-most brackets on their own lines
    [DataRow(2, 6)] // Q & R each get their own rows, plus outer [ {...} ]
    [DataRow(1, 9)] // Q gets broken up.  R stays inline.
    [DataRow(0, 14)] // Maximum expansion, basically
    public void CorrectLineCountForInlineComplexity(int maxInlineComplexity, int expectedNumberOfLines)
    {
        var inputLines = new[]
        {
            "[",
            "    { 'Q': [ {'foo': 'bar'}, 678 ], 'R': [ {}, 'asdf'] }",
            "]",
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions()
        {
            MaxTotalLineLength = 90, JsonEolStyle = EolStyle.Lf, MaxInlineComplexity = maxInlineComplexity,
            MaxCompactArrayComplexity = -1, MaxTableRowComplexity = -1,
        };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        Assert.AreEqual(expectedNumberOfLines, outputLines.Length);
    }

    [DataTestMethod]
    [DataRow(2, 5)] // 3 formatted columns across 3 lines plus the outer []
    [DataRow(1, 9)] // Each subarray gets its own line, plus the outer []
    public void CorrectLineCountForMultilineCompact(int maxCompactArrayComplexity, int expectedNumberOfLines)
    {
        var inputLines = new[]
        {
            "[",
            "    [1,2,3], [4,5,6], [7,8,9], [null,11,12], [13,14,15], [16,17,18], [19,null,21]",
            "]",
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions()
        {
            MaxTotalLineLength = 60, JsonEolStyle = EolStyle.Lf, MaxInlineComplexity = 2,
            MaxCompactArrayComplexity = maxCompactArrayComplexity, MaxTableRowComplexity = -1,
        };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        Assert.AreEqual(expectedNumberOfLines, outputLines.Length);
    }

    [DataTestMethod]
    [DataRow(120, 100, 3, 1)] // All on one line
    [DataRow(120, 90, 3, 4)] // Two row compact multiline array, + two for []
    [DataRow(120, 70, 3, 5)] // Three row compact multiline array, + two for []
    [DataRow(120, 50, 3, 9)] // Not a compact multiline array.  1 per inner array, + two for [].
    [DataRow(120, 50, 2, 6)] // Four row compact multiline array, + two for []
    [DataRow(90, 120, 3, 4)] // Two row compact multiline array, + two for []
    [DataRow(70, 120, 3, 4)] // Also two row compact multiline array, + two for []
    [DataRow(65, 120, 3, 5)] // Three row compact multiline array, + two for []
    public void CorrectLineCountForLineLength(int inlineLength, int totalLength, int minItemsPerRow,
        int expectedNumberOfLines)
    {
        var inputLines = new[]
        {
            "[",
            "    [1,2,3], [4,5,6], [7,8,9], [null,11,12], [13,14,15], [16,17,18], [19,null,21]",
            "]",
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions()
        {
            MaxInlineLength = inlineLength,
            MaxTotalLineLength = totalLength,
            JsonEolStyle = EolStyle.Lf,
            MaxInlineComplexity = 2,
            MaxCompactArrayComplexity = 2,
            MaxTableRowComplexity = 2,
            MinimumCompactArrayRowItems = minItemsPerRow,
        };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        Assert.AreEqual(expectedNumberOfLines, outputLines.Length);
    }
}
