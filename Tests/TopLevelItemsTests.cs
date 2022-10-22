using FracturedJson;

namespace Tests;

[TestClass]
public class TopLevelItemsTests
{
    [TestMethod]
    public void ThrowsIfMultipleTopLevel()
    {
        const string input = "[1,2] [3,4]";

        // There are two top-level element.  It should throw.
        var formatter = new Formatter();

        Assert.ThrowsException<FracturedJsonException>(() => formatter.Reformat(input));
        Assert.ThrowsException<FracturedJsonException>(() => formatter.Minify(input));
    }

    [TestMethod]
    public void ThrowsIfMultipleTopLevelWithComma()
    {
        const string input = "[1,2], [3,4]";

        // There are two top-level elements with a comma.  It should throw.
        var formatter = new Formatter();

        Assert.ThrowsException<FracturedJsonException>(() => formatter.Reformat(input));
        Assert.ThrowsException<FracturedJsonException>(() => formatter.Minify(input));
    }

    [TestMethod]
    public void CommentsAfterTopLevelElemPreserved()
    {
        const string input = "/*a*/ [1,2] /*b*/ //c";

        // There are two top-level elements with a comma.  It should throw.
        var formatter = new Formatter();
        formatter.Options.CommentPolicy = CommentPolicy.Preserve;
        var reformatOutput = formatter.Reformat(input);

        StringAssert.Contains(reformatOutput, "/*a*/");
        StringAssert.Contains(reformatOutput, "/*b*/");
        StringAssert.Contains(reformatOutput, "//c");

        var minifyOutput = formatter.Minify(input);

        StringAssert.Contains(minifyOutput, "/*a*/");
        StringAssert.Contains(minifyOutput, "/*b*/");
        StringAssert.Contains(minifyOutput, "//c");
    }
}
