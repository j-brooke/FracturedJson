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
                MaxTableRowComplexity = 0,
            };
        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.IsFalse(output.Contains(' '));
    }
}
