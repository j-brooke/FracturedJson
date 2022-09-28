using FracturedJson;
using Wcwidth;

namespace Tests;

/// <summary>
/// Tests for aligning data including double-wide characters.
/// </summary>

[TestClass]
public class EastAsianWideCharactersTests
{
    [TestMethod]
    public void PadsWideCharsCorrectly()
    {
        var inputLines = new[]
        {
            "[",
            "    {'Name': '李小龍', 'Job': 'Actor', 'Born': 1940},",
            "    {'Name': 'Mark Twain', 'Job': 'Writer', 'Born': 1835},",
            "    {'Name': '孫子', 'Job': 'General', 'Born': -544}",
            "]"
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts, StringLengthFunc = Formatter.StringLengthByCharCount };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // With the default StringLengthFunc, all characters are treated as having the same width as space, so
        // String.IndexOf should give the same number for each row.
        TestHelpers.TestInstancesLineUp(outputLines, "Job");
        TestHelpers.TestInstancesLineUp(outputLines, "Born");

        formatter.StringLengthFunc = WideCharStringLength;
        output = formatter.Reformat(input, 0);
        outputLines = output.TrimEnd().Split('\n');

        // In using the WideChatStringLength function, the Asian characters are each treated as 2 spaces wide.
        Assert.AreEqual(25, outputLines[1].IndexOf("Job", StringComparison.Ordinal));
        Assert.AreEqual(28, outputLines[2].IndexOf("Job", StringComparison.Ordinal));
        Assert.AreEqual(26, outputLines[3].IndexOf("Job", StringComparison.Ordinal));
    }

    public static int WideCharStringLength(string str)
    {
        return str.EnumerateRunes().Sum(rune => UnicodeCalculator.GetWidth(rune.Value));
    }
}
