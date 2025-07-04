using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FracturedJson;

namespace Tests;

[TestClass]
public class ObjectSerializationTests
{
    [TestMethod]
    public void TestSerializeCalendar()
    {
        var input = CultureInfo.InvariantCulture.Calendar;
        var fracturedJsonOptions = new FracturedJsonOptions();
        var serialOptions = new JsonSerializerOptions()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        var formatter = new Formatter() { Options = fracturedJsonOptions };
        var output = formatter.Serialize(input, 0, serialOptions);

        // Ugly Regex.  We expect, across multiple lines, to find "Eras":[1], but potentially with
        // whitespace before and after the colon.
        const string pattern =
            """
            .*"Eras"\s*:\s*\[1].*
            """;
        StringAssert.Matches(output, new Regex(pattern, RegexOptions.Multiline));
        StringAssert.Contains(output, "SolarCalendar");
    }

    [TestMethod]
    public void TestSerializeNumberFormat()
    {
        var input = CultureInfo.InvariantCulture.NumberFormat;
        var opts = new FracturedJsonOptions();

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Serialize(input, 0);

        // Ugly Regex.  We expect, across multiple lines, to find "CurrencySymbol":"\u00A4", but potentially with
        // whitespace before and after the colon.
        const string pattern =
            """
            .*"CurrencySymbol"\s*:\s*\"\\u00A4".*
            """;
        StringAssert.Matches(output, new Regex(pattern, RegexOptions.Multiline));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DataRow(null)]
    [DataRow("sandwich")]
    [DataRow(3.14)]
    [DataRow(98765432109876543210D)]
    [DataRow(9876543210987654321L)]
    public void TestSerializeSimple(object? val)
    {
        var opts = new FracturedJsonOptions();

        var formatter = new Formatter() { Options = opts };
        var output = formatter.Serialize(val, 0);

        var expected = JsonSerializer.Serialize(val);
        Assert.AreEqual(expected, output.TrimEnd());
    }
}
