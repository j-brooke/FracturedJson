using FracturedJson;

namespace Tests;

/// <summary>
/// Tests to make sure commas are only where they're supposed to be.
/// </summary>
[TestClass]
public class EndingCommaFormattingTests
{
    /// <summary>
    /// Tests that comments at the end of an expanded object/array don't cause commas before them.
    /// </summary>
    [TestMethod]
    public void NoCommasForCommentsExpanded()
    {
        var inputLines = new[]
        {
            "[",
            "/*a*/",
            "1, false",
            "/*b*/",
            "]"
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { JsonEolStyle = EolStyle.Lf, CommentPolicy = CommentPolicy.Preserve };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // Both comments here are standalone, so we're not allowed to format this as inline or compact-array.
        // The row types are dissimilar, so they won't be table-formatted either.
        Assert.AreEqual(outputLines.Length, 6);

        // There should only be one comma - between the 1 and false.
        var commaCount = output.Count(ch => ch == ',');
        Assert.AreEqual(1, commaCount);
    }

    /// <summary>
    /// Tests that comments at the end of a table-formatted object/array don't cause commas before them.
    /// </summary>
    [TestMethod]
    public void NoCommasForCommentsTable()
    {
        var inputLines = new[]
        {
            "[",
            "/*a*/",
            "[1], [false]",
            "/*b*/",
            "]"
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { JsonEolStyle = EolStyle.Lf, CommentPolicy = CommentPolicy.Preserve };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // Both comments here are standalone, so we're not allowed to format this as inline or compact-array.
        // The row types are both array, so it should be table-formatted.
        Assert.AreEqual(outputLines.Length, 6);
        StringAssert.Contains(output, "[1    ]");

        // There should only be one comma - between the 1 and 2.
        var commaCount = output.Count(ch => ch == ',');
        Assert.AreEqual(1, commaCount);
    }
}
