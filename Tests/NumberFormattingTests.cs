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

    [TestMethod]
    public void OverflowDoubleInvalidatesAlignment()
    {
        const string input = "[1e500, 4.0]";
        const string expectedOutput = "[\n    1e500, 4.0  \n]";

        // If a number is too big to fit in a 64-bit float, we shouldn't try to reformat its column/array.
        // If we did, it would turn into "Infinity", isn't a valid JSON token.
        var opts = new FracturedJsonOptions() { MaxInlineComplexity = -1, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.AreEqual(expectedOutput, output.TrimEnd());
    }

    [TestMethod]
    public void UnderflowDoubleInvalidatesAlignment()
    {
        const string input = "[1e-500, 4.0]";
        const string expectedOutput = "[\n    1e-500, 4.0   \n]";

        // If a number is too small to fit in a 64-bit float, we shouldn't try to reformat its column/array.
        // Doing so would change it to zero, which might be an unwelcome loss of precision.
        var opts = new FracturedJsonOptions() { MaxInlineComplexity = -1, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);

        Assert.AreEqual(expectedOutput, output.TrimEnd());
    }

    [TestMethod]
    public void LeftAlignMatchesExpected()
    {
        var expectedRows = new[]
        {
            "[",
            "    [123.456 , 0      , 0   ],",
            "    [234567.8, 0      , 0   ],",
            "    [3       , 0.00000, 7e2 ],",
            "    [null    , 2e-1   , 80e1],",
            "    [5.6789  , 3.5e-1 , 0   ]",
            "]",
        };

        TestAlignment(NumberListAlignment.Left, expectedRows);
    }

    [TestMethod]
    public void RightAlignMatchesExpected()
    {
        var expectedRows = new[]
        {
            "[",
            "    [ 123.456,       0,    0],",
            "    [234567.8,       0,    0],",
            "    [       3, 0.00000,  7e2],",
            "    [    null,    2e-1, 80e1],",
            "    [  5.6789,  3.5e-1,    0]",
            "]",
        };

        TestAlignment(NumberListAlignment.Right, expectedRows);
    }

    [TestMethod]
    public void DecimalAlignMatchesExpected()
    {
        var expectedRows = new[]
        {
            "[",
            "    [   123.456 , 0      ,  0  ],",
            "    [234567.8   , 0      ,  0  ],",
            "    [     3     , 0.00000,  7e2],",
            "    [  null     , 2e-1   , 80e1],",
            "    [     5.6789, 3.5e-1 ,  0  ]",
            "]",
        };

        TestAlignment(NumberListAlignment.Decimal, expectedRows);
    }

    [TestMethod]
    public void NormalizeAlignMatchesExpected()
    {
        var expectedRows = new[]
        {
            "[",
            "    [   123.4560, 0.00,   0],",
            "    [234567.8000, 0.00,   0],",
            "    [     3.0000, 0.00, 700],",
            "    [  null     , 0.20, 800],",
            "    [     5.6789, 0.35,   0]",
            "]",
        };

        TestAlignment(NumberListAlignment.Normalize, expectedRows);
    }

    private static void TestAlignment(NumberListAlignment align, string[] expectedRows)
    {

        var input = string.Join(string.Empty, _numberTable);
        var opts = new FracturedJsonOptions()
        {
            MaxTotalLineLength = 60,
            JsonEolStyle = EolStyle.Lf,
            OmitTrailingWhitespace = true,
            NumberListAlignment = align
        };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputRows = output.TrimEnd().Split('\n');

        CollectionAssert.AreEqual(expectedRows, outputRows);
    }


    private static readonly string[] _numberTable = new[]
    {
        "[",
        "    [ 123.456, 0, 0 ],",
        "    [ 234567.8, 0, 0 ],",
        "    [ 3, 0.00000, 7e2 ],",
        "    [ null, 2e-1, 80e1 ],",
        "    [ 5.6789, 3.5e-1, 0 ]",
        "]",
    };
}
