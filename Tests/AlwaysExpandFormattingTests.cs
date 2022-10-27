using FracturedJson;

namespace Tests;

[TestClass]
public class AlwaysExpandFormattingTests
{
    [TestMethod]
    public void AlwaysExpandDepthHonored()
    {
        var inputLines = new[]
        {
            "[",
            "[ {'x':1}, false ],",
            "{ 'a':[2], 'b':[3] }",
            "]"
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { MaxInlineComplexity = 100, MaxTotalLineLength = int.MaxValue };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // With high maximum complexity and long line length, it should all be in one line.
        Assert.AreEqual(1, outputLines.Length);

        formatter.Options = opts with { AlwaysExpandDepth = 0 };
        output = formatter.Reformat(input, 0);
        outputLines = output.TrimEnd().Split('\n');

        // If we force expanding at depth 0, we should get 4 lines (more or less like the input).
        Assert.AreEqual(4, outputLines.Length);

        formatter.Options = opts with { AlwaysExpandDepth = 1 };
        output = formatter.Reformat(input, 0);
        outputLines = output.TrimEnd().Split('\n');

        // If we force expanding at depth 1, we'll get lots of lines.
        Assert.AreEqual(10, outputLines.Length);
    }

    [TestMethod]
    public void AlwaysExpandDepthDoesntPreventTable()
    {
        const string input = "[ [1, 22, 9 ], [333, 4, 9 ] ]";

        // With AlwaysExpandDepth=0, this whole line isn't allowed to be inlined.  But there's no reason
        // why it shouldn't qualify for table formatting.  So the 1 should be padded to the size of 333, and
        // 4 should be padded to the size of 22.  The commas and 9s should line up.
        var opts = new FracturedJsonOptions() { JsonEolStyle = EolStyle.Lf, AlwaysExpandDepth = 0 };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        Assert.AreEqual(4, outputLines.Length);
        TestHelpers.TestInstancesLineUp(outputLines, ",");
        TestHelpers.TestInstancesLineUp(outputLines, "9");
    }
}