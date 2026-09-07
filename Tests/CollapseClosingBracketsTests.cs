using FracturedJson;

namespace Tests;

/// <summary>
/// Tests for CollapseClosingBrackets: a container's close can share the last child's line when the
/// last item is a real value with no postfix comment and the combined line still fits.
/// </summary>
[TestClass]
public class CollapseClosingBracketsTests
{
    [TestMethod]
    public void CollapsesNestedExpandedBrackets()
    {
        const string input = "[[1, 2]]";
        var output = Reformat(input, FullyExpanded());
        var lines = Lines(output);

        Assert.AreEqual(4, lines.Length);
        Assert.AreEqual("[", lines[0]);
        Assert.AreEqual("    [", lines[1]);
        Assert.AreEqual("        1,", lines[2]);
        Assert.AreEqual("        2] ]", lines[3]);
    }

    [TestMethod]
    public void DoesNotCollapseWhenOptionIsOff()
    {
        const string input = "[[1, 2]]";
        var output = Reformat(input, FullyExpanded() with { CollapseClosingBrackets = false });

        const string expected = """
            [
                [
                    1,
                    2
                ]
            ]
            """;
        Assert.AreEqual(expected, output.TrimEnd());
    }

    [TestMethod]
    public void CollapsesOntoInlinedLastChild()
    {
        const string input = "{\"a\":{\"b\":[1,2,3]}}";
        var opts = RootExpanded();
        var output = Reformat(input, opts);

        const string expected = """
            {
                "a": { "b": [1, 2, 3] } }
            """;
        Assert.AreEqual(expected, output.TrimEnd());
    }

    [TestMethod]
    public void CollapsesCompactArrayClose()
    {
        const string input = "[1,2,3,4,5,6,7,8,9,10]";
        var opts = new FracturedJsonOptions
        {
            JsonEolStyle = EolStyle.Lf,
            CollapseClosingBrackets = true,
            MaxInlineComplexity = 0,
            MaxCompactArrayComplexity = 1,
            MaxTableRowComplexity = -1,
            MaxTotalLineLength = 20,
        };
        var output = Reformat(input, opts);
        var lines = Lines(output);

        Assert.IsTrue(lines.Length >= 3);
        Assert.AreEqual("[", lines[0]);
        StringAssert.EndsWith(lines[^1].TrimEnd(), "]");
        Assert.IsFalse(lines[^1].Trim() == "]");
        StringAssert.Contains(lines[^1], "10");
    }

    [TestMethod]
    public void CollapsesTableClose()
    {
        const string input = "[[1, 22],[333, 4]]";
        var opts = new FracturedJsonOptions
        {
            JsonEolStyle = EolStyle.Lf,
            CollapseClosingBrackets = true,
            AlwaysExpandDepth = 0,
            MaxInlineComplexity = 1,
            MaxCompactArrayComplexity = 0,
        };
        var output = Reformat(input, opts);

        const string expected = """
            [
                [  1, 22],
                [333,  4]   ]
            """;
        Assert.AreEqual(expected, output.TrimEnd());
    }

    [TestMethod]
    public void DoesNotCollapseOntoPostfixComment()
    {
        const string input = """
            [
                1,
                2 /* keep */
            ]
            """;
        var opts = FullyExpanded() with { CommentPolicy = CommentPolicy.Preserve };
        var output = Reformat(input, opts);
        var lines = Lines(output);

        Assert.AreEqual("]", lines[^1]);
        StringAssert.Contains(lines[^2], "/* keep */");
    }

    [TestMethod]
    public void DoesNotCollapseOntoStandaloneComment()
    {
        const string input = """
            [
                1,
                2
                /* trailing */
            ]
            """;
        var opts = FullyExpanded() with { CommentPolicy = CommentPolicy.Preserve };
        var output = Reformat(input, opts);
        var lines = Lines(output);

        Assert.AreEqual("]", lines[^1]);
        StringAssert.Contains(lines[^2], "/* trailing */");
    }

    [TestMethod]
    public void DoesNotCollapseWhenLineWouldExceedMax()
    {
        const string input = "{\"k\": [1, 2, 3, 4, 5, 6, 7, 8]}";
        var opts = RootExpanded() with { MaxTotalLineLength = 34 };
        var output = Reformat(input, opts);

        const string expected = """
            {
                "k": [1, 2, 3, 4, 5, 6, 7, 8]
            }
            """;
        Assert.AreEqual(expected, output.TrimEnd());
        Assert.IsTrue(Lines(output).All(line => line.Length <= opts.MaxTotalLineLength));
    }

    [TestMethod]
    public void CollapsedLinesStayWithinMaxTotalLineLength()
    {
        const string input = "[[[1,2],[3,4]],[[5,6],[7,8]]]";
        var opts = FullyExpanded() with { MaxTotalLineLength = 40 };
        var output = Reformat(input, opts);
        var lines = Lines(output);

        Assert.IsTrue(lines.Any(line => line.Contains(']')));
        Assert.IsTrue(lines.All(line => line.Length <= opts.MaxTotalLineLength));
    }

    [TestMethod]
    public void CollapsesOntoPropertyAlignedLastChild()
    {
        const string input = """
            {
                "short": 1,
                "longerName": { "x": 1 }
            }
            """;
        var opts = new FracturedJsonOptions
        {
            JsonEolStyle = EolStyle.Lf,
            CollapseClosingBrackets = true,
            AlwaysExpandDepth = 0,
            MaxPropNamePadding = 16,
            MaxTotalLineLength = 80,
        };
        var output = Reformat(input, opts);
        var lines = Lines(output);

        const string expected = """
            {
                "short"     : 1,
                "longerName": {"x": 1} }
            """;
        Assert.AreEqual(expected, output.TrimEnd());
    }

    [TestMethod]
    public void StringAndWriterBuffersMatchWhenCollapsing()
    {
        const string input = "[[1, 2, 3], {\"a\": [4, 5]}]";
        var opts = FullyExpanded() with { MaxTotalLineLength = 50 };
        var formatter = new Formatter { Options = opts };

        var asString = formatter.Reformat(input, 0);
        using var writer = new StringWriter();
        formatter.Reformat(input, 0, writer);
        Assert.AreEqual(asString, writer.ToString());
    }

    private static FracturedJsonOptions FullyExpanded() => new()
    {
        JsonEolStyle = EolStyle.Lf,
        CollapseClosingBrackets = true,
        MaxInlineComplexity = -1,
        MaxCompactArrayComplexity = -1,
        MaxTableRowComplexity = -1,
    };

    /// <summary>
    /// Expand only the root so children may still inline.  Table formatting is disabled so the last
    /// child goes through FormatItem rather than a table row (which would add dummy-comma padding).
    /// </summary>
    private static FracturedJsonOptions RootExpanded() => new()
    {
        JsonEolStyle = EolStyle.Lf,
        CollapseClosingBrackets = true,
        AlwaysExpandDepth = 0,
        MaxTableRowComplexity = -1,
        MaxCompactArrayComplexity = -1,
        MaxTotalLineLength = 80,
    };

    private static string Reformat(string input, FracturedJsonOptions opts)
    {
        return new Formatter { Options = opts }.Reformat(input, 0);
    }

    private static string[] Lines(string output)
    {
        return output.TrimEnd().Split('\n');
    }
}
