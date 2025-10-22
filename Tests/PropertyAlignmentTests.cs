using FracturedJson;

namespace Tests;

[TestClass]
public class PropertyAlignmentTests
{
    [TestMethod]
    public void PropValuesAligned()
    {
        const string input =
            """
            {
                "num": 14,
                "string": "testing property alignment",
                "arrayWithLongName": [null, null, null]
            }
            """;

        var opts = new FracturedJsonOptions()
        {
            MaxPropNamePadding = 15,
            ColonBeforePropNamePadding = false,
            MaxInlineComplexity = -1,
            MaxCompactArrayComplexity = -1,
        };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // This object should be expanded with the property values and colons aligned.  The array should be expanded
        // as well.
        Assert.AreEqual(9, outputLines.Length);
        TestHelpers.TestInstancesLineUp(outputLines, ":");
    }

    [TestMethod]
    public void PropValuesAlignedButNotColons()
    {
        const string input =
            """
            {
                "num": 14,
                "string": "testing property alignment",
                "arrayWithLongName": [null, null, null]
            }
            """;

        var opts = new FracturedJsonOptions()
        {
            MaxPropNamePadding = 15,
            ColonBeforePropNamePadding = true,
            MaxInlineComplexity = -1,
            MaxCompactArrayComplexity = -1,
        };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // This object should be expanded with the property values, but the colons should hug the prop names instead
        // of being aligned.
        Assert.AreEqual(9, outputLines.Length);
        StringAssert.Contains(outputLines[1], "\"num\":");
        StringAssert.Contains(outputLines[2], "\"string\":");
        StringAssert.Contains(outputLines[3], "\"arrayWithLongName\":");
        Assert.AreEqual(outputLines[1].IndexOf("14", StringComparison.InvariantCulture),
            outputLines[2].IndexOf("\"testing", StringComparison.InvariantCulture));
        Assert.AreEqual(outputLines[1].IndexOf("14", StringComparison.InvariantCulture),
            outputLines[3].IndexOf('[', StringComparison.InvariantCulture));
    }

    [TestMethod]
    public void DontAlignPropValsWhenTooMuchPaddingRequired()
    {
        const string input =
            """
            {
                "num": 14,
                "string": "testing property alignment",
                "arrayWithLongName": [null, null, null]
            }
            """;

        var opts = new FracturedJsonOptions()
        {
            MaxPropNamePadding = 12,
            ColonBeforePropNamePadding = false,
            MaxInlineComplexity = -1,
            MaxCompactArrayComplexity = -1,
        };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // This object should be expanded but the property values shouldn't be aligned since the length of the
        // prop names differ by more than MaxPropNamePadding.
        Assert.AreEqual(9, outputLines.Length);
        StringAssert.Contains(outputLines[1], "\"num\": 14,");
        StringAssert.Contains(outputLines[2], "\"string\": \"testing");
        StringAssert.Contains(outputLines[3], "\"arrayWithLongName\": [");
    }
}
