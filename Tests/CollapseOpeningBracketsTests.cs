using FracturedJson;

namespace Tests;

[TestClass]
public class CollapseOpeningBracketsTests
{
    [TestMethod]
    public void CollapseIfPropNameIsShortEnough()
    {
        var options = CommonOptions;
        var formatter = new Formatter() { Options = options };
        var output = formatter.Reformat(_testJson);
        var outputLines = output.TrimEnd().Split('\n');

        Assert.AreEqual(18, outputLines.Length);

        // The "q" and the /*t*/ get collapsed since their containers' bracket lines are short enough that they
        // can start at the same position as they normally would.
        Assert.AreEqual("{     \"q\": [/*t*/ [", outputLines[0]);

        // Likewise, the first [ inside the "r" array can be collapsed.  It just barely fits.
        StringAssert.StartsWith(outputLines[6], "      \"r\": [[      1,");

        // The first [ inside the "ss" array is not collapsed - the prop name, colon, and bracket are 7 wide, so there's
        // no room.
        Assert.AreEqual("      \"ss\": [", outputLines[11]);
    }

    [TestMethod]
    public void PropNamePaddingAffectsDecision()
    {
        var options = CommonOptions with { MaxPropNamePadding = 20 };
        var formatter = new Formatter() { Options = options };
        var output = formatter.Reformat(_testJson);
        var outputLines = output.TrimEnd().Split('\n');

        Assert.AreEqual(20, outputLines.Length);

        // The /*t*/ can't follow the "q", since prop name padding is making the prop name, colon, and bracket take
        // too much space.
        Assert.AreEqual("{     \"q\" : [", outputLines[0]);
    }

    [TestMethod]
    public void TabsPreventCollapse()
    {
        var options = CommonOptions with { UseTabToIndent = true };
        var formatter = new Formatter() { Options = options };
        var output = formatter.Reformat(_testJson);
        var outputLines = output.TrimEnd().Split('\n');

        // Despite some lines having room for a collapse, tabs prevent it.
        Assert.AreEqual(23, outputLines.Length);
    }

    private static FracturedJsonOptions CommonOptions => new()
    {
        IndentSpaces = 6,
        CollapseOpeningBrackets = true,
        MaxTotalLineLength = 80,
        MaxPropNamePadding = 0,
        CommentPolicy = CommentPolicy.Preserve,
        NestedBracketPadding = false,
    };

    private const string _testJson =
        """
        {
            "q" : [
                /*t*/ [
                     1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17,
                    18, 19, 20
                ],
                [21, 22, 23, 24, 25, 26, 27, 28, 29, 30]
            ],
            "r" : [
                [
                     1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17,
                    18, 19, 20
                ],
                [21, 22, 23, 24, 25, 26, 27, 28, 29, 30]
            ],
            "ss": [
                [
                     1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17,
                    18, 19, 20
                ],
                [21, 22, 23, 24, 25, 26, 27, 28, 29, 30]
            ]
        }
        """;
}
