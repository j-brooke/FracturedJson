namespace Tests;

/// <summary>
/// Utility functions to help with unit tests
/// </summary>
public static class TestHelpers
{
    public static void TestInstancesLineUp(string[] lines, string substring)
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
