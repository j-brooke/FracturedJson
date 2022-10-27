using FracturedJson;

namespace Tests;

/// <summary>
/// Unit tests for various padding functionality and maybe indentation
/// </summary>
[TestClass]
public class PadFormattingTests
{
    [TestMethod]
    public void NoSpacesAnywhere()
    {
        var filename = Path.Combine("StandardJsonFiles", "1.json");
        var input = File.ReadAllText(filename);

        // Turn off all padding (except comments - not worrying about that here).  Use tabs to indent.  Disable
        // compact multiline arrays.  There will be no spaces anywhere.
        var opts = new FracturedJsonOptions()
            {
                UseTabToIndent = true,
                ColonPadding = false,
                CommaPadding = false,
                NestedBracketPadding = false,
                SimpleBracketPadding = false,
                MaxCompactArrayComplexity = 0,
                MaxTableRowComplexity = -1,
            };
        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.IsFalse(output.Contains(' '));
    }

    [TestMethod]
    public void SimpleBracketPaddingWorksForTables()
    {
        const string input = "[[1, 2],[3, 4]]";

        var opts = new FracturedJsonOptions()
        {
            JsonEolStyle = EolStyle.Lf,
            MaxInlineComplexity = 1,
            SimpleBracketPadding = true,
        };

        // Limit the complexity to make sure we format this as a table, but set SimpleBracketPadding to true.
        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // There should be spaces between the brackets and the numbers.
        Assert.AreEqual(4, outputLines.Length);
        StringAssert.Contains(outputLines[1], "[ 1, 2 ]");
        StringAssert.Contains(outputLines[2], "[ 3, 4 ]");

        formatter.Options.SimpleBracketPadding = false;
        output = formatter.Reformat(input, 0);
        outputLines = output.TrimEnd().Split('\n');

        // There should NOT be spaces between the brackets and the numbers.
        Assert.AreEqual(4, outputLines.Length);
        StringAssert.Contains(outputLines[1], "[1, 2]");
        StringAssert.Contains(outputLines[2], "[3, 4]");
    }
}
