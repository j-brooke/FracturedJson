using FracturedJson;
using Wcwidth;

namespace Tests;

/// <summary>
/// Tests for aligning data including double-wide characters.  FracturedJson doesn't handle this in the core
/// library, but provides the functionality via the StringLengthFunc property.  In version 2, this was built in,
/// but that meant that any particular version of the FracturedJson library was couple to a version of Wcwidth.
/// By switching the burden to the app developer, I'm giving them more freedom to pick their own width logic,
/// while streamlining the core library.
///
/// Still, it is a feature we want to be sure works.  That's what these tests are for.
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
        // Whether these line up visually for you depends on your font and the rendering policies of your app.
        // (It looks right on a Mac terminal.)
        Assert.AreEqual(25, outputLines[1].IndexOf("Job", StringComparison.Ordinal));
        Assert.AreEqual(28, outputLines[2].IndexOf("Job", StringComparison.Ordinal));
        Assert.AreEqual(26, outputLines[3].IndexOf("Job", StringComparison.Ordinal));
    }

    public static int WideCharStringLength(string str)
    {
        return str.EnumerateRunes().Sum(rune => UnicodeCalculator.GetWidth(rune.Value));
    }
}
