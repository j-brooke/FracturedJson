using System.Globalization;
using FracturedJson;

namespace Tests;

[TestClass]
public class BufferEquivalenceTests
{
    /// <summary>
    /// Tests that both overloads of Reformat produce the same output.
    /// </summary>
    [TestMethod]
    public void ReformatSameForBothOverrides()
    {
        var formatter = new Formatter();
        var fileData = File.ReadAllText(Path.Combine("StandardJsonFiles", "0.json"));

        var formattedAsString = formatter.Reformat(fileData, 0);

        using var memWriter = new StringWriter();
        formatter.Reformat(fileData, 0, memWriter);
        var writerString = memWriter.ToString();

        Assert.AreEqual(writerString, formattedAsString);
    }

    /// <summary>
    /// Tests that both overloads of Serialize produce the same output.
    /// </summary>
    [TestMethod]
    public void SerializeSameForBothOverrides()
    {
        var formatter = new Formatter();
        var input = CultureInfo.InvariantCulture.NumberFormat;

        var formattedAsString = formatter.Serialize(input, 0);

        using var memWriter = new StringWriter();
        formatter.Serialize(input, 0, memWriter);
        var writerString = memWriter.ToString();

        Assert.AreEqual(writerString, formattedAsString);
    }
}

