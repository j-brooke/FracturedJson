using FracturedJson;

namespace Tests;

[TestClass]
public class TableFormattingTests
{
    [TestMethod]
    public void NestedElementsLineUp()
    {
        var inputLines = new[]
        {
            "{",
            "    'Rect' : { 'position': {'x': -44, 'y':  3.4}, 'color': [0, 255, 255] }, ",
            "    'Point': { 'position': {'y': 22, 'z': 3} }, ",
            "    'Oval' : { 'position': {'x': 140, 'y':  0.04}, 'color': '#7f3e96' }  ",
            "}",
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        // With default options (except EOF, this will be neatly formatted as a table.
        var opts = new FracturedJsonOptions() { JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // Everything should line up.
        TestInstancesLineUp(outputLines, "x");
        TestInstancesLineUp(outputLines, "y");
        TestInstancesLineUp(outputLines, "z");
        TestInstancesLineUp(outputLines, "position");
        TestInstancesLineUp(outputLines, "color");

        // The numbers of the y column will be justified.
        StringAssert.Contains(outputLines[2], "22.00,");
    }


    [TestMethod]
    public void NestedElementsCompactWhenNeeded()
    {
        var inputLines = new[]
        {
            "{",
            "    'Rect' : { 'position': {'x': -44, 'y':  3.4}, 'color': [0, 255, 255] }, ",
            "    'Point': { 'position': {'y': 22, 'z': 3} }, ",
            "    'Oval' : { 'position': {'x': 140, 'y':  0.04}, 'color': '#7f3e96' }  ",
            "}",
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        // Smaller rows, so there's not enough room to do a full table.
        var opts = new FracturedJsonOptions() { MaxTotalLineLength = 77, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // Since the available size is reduced, x,y,z will no longer line up, but position and color will.
        TestInstancesLineUp(outputLines, "position");
        TestInstancesLineUp(outputLines, "color");

        // The numbers of the y column aren't justified, so the input value is used.
        StringAssert.Contains(outputLines[2], "22,");
    }


    [TestMethod]
    public void FallBackOnInlineIfNeeded()
    {
        var inputLines = new[]
        {
            "{",
            "    'Rect' : { 'position': {'x': -44, 'y':  3.4}, 'color': [0, 255, 255] }, ",
            "    'Point': { 'position': {'y': 22, 'z': 3} }, ",
            "    'Oval' : { 'position': {'x': 140, 'y':  0.04}, 'color': '#7f3e96' }  ",
            "}",
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        // In this case, it's too small to do any table formatting.  But each row should still be inlined.
        var opts = new FracturedJsonOptions() { MaxTotalLineLength = 74, JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // All rows should be inlined, so a total of 5 rows.
        Assert.AreEqual(5, outputLines.Length);

        // Not even position lines up here.
        Assert.AreNotEqual(outputLines[1].IndexOf("position", StringComparison.Ordinal),
            outputLines[2].IndexOf("position", StringComparison.Ordinal));
    }


    [TestMethod]
    public void TablesWithCommentsLineUp()
    {
        var inputLines = new[]
        {
            "{",
            "'Firetruck': /* red */ { 'color': '#CC0000' }, ",
            "'Dumptruck': /* yellow */ { 'color': [255, 255, 0] }, ",
            "'Godzilla': /* green */  { 'color': '#336633' },  // Not a truck",
            "/* ! */ 'F150': { 'color': null } ",
            "}"
        };
        var input = string.Join("\n", inputLines).Replace('\'', '"');

        // Need to be wide enough and allow comments.
        var opts = new FracturedJsonOptions() { MaxTotalLineLength = 100, CommentPolicy = CommentPolicy.Preserve,
            JsonEolStyle = EolStyle.Lf };

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        var outputLines = output.TrimEnd().Split('\n');

        // All rows should be inlined, so a total of 5 rows.
        Assert.AreEqual(6, outputLines.Length);

        // Lots of stuff to line up here.
        TestInstancesLineUp(outputLines, "\"");
        TestInstancesLineUp(outputLines, ":");
        TestInstancesLineUp(outputLines, " {");
        TestInstancesLineUp(outputLines, " }");
        TestInstancesLineUp(outputLines, "color");
    }


    private void TestInstancesLineUp(string[] lines, string substring)
    {
        var indices = lines.Select(str => str.IndexOf(substring, StringComparison.Ordinal))
            .ToArray();
        var indexCount = indices
            .Where(num => num >= 0)
            .Distinct()
            .Count();
        Assert.AreEqual(1, indexCount);
    }
}
