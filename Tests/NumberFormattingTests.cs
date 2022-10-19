using FracturedJson;

namespace Tests;

/// <summary>
/// Tests that numbers are formatted as expected - or left alone when appropriate.
/// </summary>
[TestClass]
public class NumberFormattingTests
{
    [TestMethod]
    public void InlineArrayDoesntJustifyNumbers()
    {
        const string input = "[1, 2.1, 3, -99]";
        const string expectedOutput = "[1, 2.1, 3, -99]";

        // With default options, this will be inlined, so no attempt is made to reformat or justify the numbers.
        var opts = new FracturedJsonOptions();

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.AreEqual(expectedOutput, output.TrimEnd());
    }

    [TestMethod]
    public void CompactArrayDoesJustifyNumbers()
    {
        const string input = "[1, 2.1, 3, -99]";
        const string expectedOutput = "[\n      1.0,   2.1,   3.0, -99.0\n]";

        // Here, it's formatted as a compact multiline array (but not really multiline).  All elements are formatted
        // alike, which means padding spaces on the left and zeros on the right.
        var opts = new FracturedJsonOptions() { MaxInlineComplexity = -1, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.AreEqual(expectedOutput, output.TrimEnd());
    }

    [TestMethod]
    public void TableArrayDoesJustifyNumbers()
    {
        const string input = "[[1, 2.1, 3, -99],[5, 6, 7, 8]]";
        const string expectedOutput =
            "[\n" +
            "    [1, 2.1, 3, -99], \n" +
            "    [5, 6.0, 7,   8]  \n" +
            "]";

        // Since this is table formatting, each column is consistent, but not siblings in the same array.
        var opts = new FracturedJsonOptions() { MaxInlineComplexity = -1, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.AreEqual(expectedOutput, output.TrimEnd());
    }

    [TestMethod]
    public void DontJustifyOptionRespected()
    {
        const string input = "[1, 2.1, 3, -99]";
        const string expectedOutput = "[\n    1  , 2.1, 3  , -99\n]";

        // Here, it's formatted as a compact multiline array (but not really multiline).  But since we're telling it
        // not to justify numbers, they're treated like text: left-aligned and space-padded.
        var opts = new FracturedJsonOptions()
            { MaxInlineComplexity = -1, DontJustifyNumbers = true, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.AreEqual(expectedOutput, output.TrimEnd());
    }

    [TestMethod]
    public void BigNumbersInvalidateAlignment1()
    {
        const string input = "[1, 2.1, 3, 1e+99]";
        const string expectedOutput = "[\n    1    , 2.1  , 3    , 1e+99\n]";

        // If there's a number that requires an "E", don't try to justify the numbers.
        var opts = new FracturedJsonOptions()
            { MaxInlineComplexity = -1, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.AreEqual(expectedOutput, output.TrimEnd());
    }

    [TestMethod]
    public void BigNumbersInvalidateAlignment2()
    {
        const string input = "[1, 2.1, 3, 1234567890123456]";
        const string expectedOutput = "[\n    1               , 2.1             , 3               , 1234567890123456\n]";

        // If there's a number with too many significant digits, don't try to justify the numbers.
        var opts = new FracturedJsonOptions()
            { MaxInlineComplexity = -1, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.AreEqual(expectedOutput, output.TrimEnd());
    }

    [TestMethod]
    public void NullsRespectedWhenAligningNumbers()
    {
        const string input = "[1, 2, null, -99]";
        const string expectedOutput = "[\n       1,    2, null,  -99\n]";

        // In general, if an array contains stuff other than numbers, we don't try to justify them.  Null is an
        // exception though: an array of numbers and nulls should be justified as numbers.
        var opts = new FracturedJsonOptions() { MaxInlineComplexity = -1, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.AreEqual(expectedOutput, output.TrimEnd());
    }

}
