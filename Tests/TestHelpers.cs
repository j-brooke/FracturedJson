namespace Tests;

/// <summary>
/// Utility functions to help with unit tests
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Asserts that all instances of <paramref name="seekStrings"/> in <paramref name="lines"/> occur at
    /// the same character position.
    /// </summary>
    public static void TestInstancesLineUp(string[] lines, params string[] seekStrings)
    {
        var indexCount = CountDistinctColumns(lines, seekStrings);
        Assert.AreEqual(1, indexCount);
    }

    /// <summary>
    /// Finds the number of distinct positions at which any of <paramref name="seekStrings"/> occur in any of
    /// <paramref name="lines"/>.  Lines where no seek string exists are ignored.  If all instances occur at the same
    /// index, this returns 1.
    /// </summary>
    public static int CountDistinctColumns(string[] lines, params string[] seekStrings)
    {
        var indices = seekStrings.SelectMany(seek =>
            lines.Select(str => str.IndexOf(seek, StringComparison.Ordinal)));
        var indexCount = indices
            .Where(num => num >= 0)
            .Distinct()
            .Count();
        return indexCount;
    }
}
