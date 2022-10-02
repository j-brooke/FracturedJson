using FracturedJson;

namespace Tests;

/// <summary>
/// Tests having to do with formatting with comments.
/// </summary>
[TestClass]
public class CommentFormattingTests
{

    [TestMethod]
    public void PrePostCommentsStayWithElems()
    {
        var inputLines = new[]
        {
            "{",
            "    /*1*/ 'a': [true, true], /*2*/",
            "    'b': [false, false], ",
            "    /*3*/ 'c': [false, true] /*4*/",
            "}"
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { CommentPolicy = CommentPolicy.Preserve, JsonEolStyle = EolStyle.Lf,
            MaxInlineComplexity = 2, };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // With these options, they should all be written on one line.
        Assert.AreEqual(1, outputLines.Length);

        formatter.Options = opts with { MaxInlineComplexity = 1 };
        output = formatter.Reformat(input, 0);
        outputLines = output.TrimEnd().Split('\n');

        // With MaxInlineComplexity=1, the output should be much like the input (except with a little padding).
        // Importantly, the comment 2 should stay with the 'a' line, and comment 3 should be with 'c'.
        Assert.AreEqual(5, outputLines.Length);
        StringAssert.Contains(outputLines[1], "\"a\"");
        StringAssert.Contains(outputLines[1], "/*2*/");
        StringAssert.Contains(outputLines[3], "\"c\"");
        StringAssert.Contains(outputLines[3], "/*3*/");

        // With no inlining possible, every subarray element gets its own line.  But the comments before the property
        // names and after the array-ending brackets need to stick to those things.
        formatter.Options = opts with
            { MaxInlineComplexity = 0, MaxCompactArrayComplexity = 0, MaxTableRowComplexity = 0};
        output = formatter.Reformat(input, 0);
        outputLines = output.TrimEnd().Split('\n');

        Assert.AreEqual(14, outputLines.Length);
        StringAssert.Contains(outputLines[1], "/*1*/ \"a\"");
        StringAssert.Contains(outputLines[4], "] /*2*/,");
        StringAssert.Contains(outputLines[9], "/*3*/ \"c\"");
        StringAssert.Contains(outputLines[12], "] /*4*/");
    }

    [TestMethod]
    public void BlankLinesForceExpanded()
    {
        var inputLines = new[]
        {
            "    [ 1,",
            "    ",
            "    2 ]",
        };

        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // By default, blank lines are ignored like any other whitespace, so this whole thing gets inlined.
        Assert.AreEqual(1, outputLines.Length);

        formatter.Options = opts with { PreserveBlankLines = true };
        output = formatter.Reformat(input, 0);
        outputLines = output.TrimEnd().Split('\n');

        // If we're preserving blank lines, the array has to be written as expanded, so 1 line for each element,
        // 1 for the blank line, and 1 each for [].
        Assert.AreEqual(5, outputLines.Length);
    }

    [TestMethod]
    public void CanInlineMiddleCommentsIfNoLineBreak()
    {
        var inputLines = new[]
        {
            "{'a': /*1*/",
            "[true,true]}",
        };

        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { CommentPolicy = CommentPolicy.Preserve, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // There's a comment between the property name and the prop value, but it doesn't require line breaks,
        // so the whole thing can be written inline.
        Assert.AreEqual(1, outputLines.Length);
        StringAssert.Contains(outputLines[0], "/*1*/");

        // If we disallow inlining, it'll be handled as a compact multiline array.
        formatter.Options = opts with { MaxInlineComplexity = 0 };
        output = formatter.Reformat(input, 0);
        outputLines = output.TrimEnd().Split('\n');
        StringAssert.Contains(outputLines[1], "\"a\": /*1*/ [" );
    }

    [TestMethod]
    public void SplitWhenMiddleCommentRequiresBreak1()
    {
        var inputLines = new[]
        {
            "{'a': //1",
            "[true,true]}",
        };

        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { CommentPolicy = CommentPolicy.Preserve, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // Since there's a comment that requires a line break between the property name and its value, the
        // comment gets put on a new line with an extra indent level, and the value is written as expanded,
        // also with the extra indent level
        Assert.AreEqual(8, outputLines.Length);
        Assert.AreEqual(4, outputLines[1].IndexOf("\"a\"", StringComparison.Ordinal));
        Assert.AreEqual(8, outputLines[2].IndexOf("//1", StringComparison.Ordinal));
        Assert.AreEqual(8, outputLines[3].IndexOf("[", StringComparison.Ordinal));
    }


    [TestMethod]
    public void SplitWhenMiddleCommentRequiresBreak2()
    {
        var inputLines = new[]
        {
            "{'a': /*1",
            "2*/ [true,true]}",
        };

        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { CommentPolicy = CommentPolicy.Preserve, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // Since there's a comment that requires a line break between the property name and its value, the
        // comment gets put on a new line with an extra indent level, and the value is written as expanded,
        // also with the extra indent level
        Assert.AreEqual(9, outputLines.Length);
        Assert.AreEqual(4, outputLines[1].IndexOf("\"a\"", StringComparison.Ordinal));
        Assert.AreEqual(8, outputLines[2].IndexOf("/*1", StringComparison.Ordinal));
        Assert.AreEqual(8, outputLines[4].IndexOf("[", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MultilineCommentsPreserveRelativeSpace()
    {
        var inputLines = new[]
        {
            "[ 1,",
            "  /* +",
            "     +",
            "     + */",
            "  2]",
        };

        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions() { CommentPolicy = CommentPolicy.Preserve, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // The +'s should stay lined up in the output.
        Assert.AreEqual(7, outputLines.Length);
        TestHelpers.TestInstancesLineUp(outputLines, "+");
    }

    [TestMethod]
    public void AmbiguousCommentsInArraysRespectCommas()
    {
        var inputLines = new[]
        {
            "[ [ 'a' /*1*/, 'b' ],",
            "  [ 'c', /*2*/ 'd' ] ]",
        };

        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions()
        {
            CommentPolicy = CommentPolicy.Preserve,
            JsonEolStyle = EolStyle.Lf,
            AlwaysExpandDepth = 99,
        };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // We split all of the elements onto separate lines, but the comments should stay with them.  The comment
        // should stick to the element that's on the same side of the comma.
        Assert.AreEqual(10, outputLines.Length);
        StringAssert.Contains(output, "\"a\" /*1*/,");
        StringAssert.Contains(output, "/*2*/ \"d\"");
    }

    [TestMethod]
    public void AmbiguousCommentsInObjectsRespectCommas()
    {
        var inputLines = new[]
        {
            "[ { 'a':'a' /*1*/, 'b':'b' },",
            "  { 'c':'c', /*2*/ 'd':'d'} ]",
        };

        var input = string.Join("\n", inputLines).Replace('\'', '"');

        var opts = new FracturedJsonOptions()
        {
            CommentPolicy = CommentPolicy.Preserve,
            JsonEolStyle = EolStyle.Lf,
            AlwaysExpandDepth = 99,
        };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // We split all of the elements onto separate lines, but the comments should stay with them.  The comment
        // should stick to the element that's on the same side of the comma.
        Assert.AreEqual(10, outputLines.Length);
        StringAssert.Contains(output, "\"a\" /*1*/,");
        StringAssert.Contains(output, "/*2*/ \"d\"");
    }
}
