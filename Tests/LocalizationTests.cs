using System.Globalization;
using FracturedJson;

namespace Tests;

/// <summary>
/// Unit tests to ensure that everything works regardless of locale. (Really this is just about parsing
/// and formatting numbers consistently.
/// </summary>
[TestClass]
public class LocalizationTests
{
    [TestMethod]
    public void LocaleDoesntMatter()
    {
        var inputRows = new[]
        {
            "[",
            "    { \"a\": 0, \"b\": 7.8 },",
            "    { \"a\": 9988776, \"b\": -0.06 }",
            "]",
        };

        var input = string.Join(string.Empty, inputRows);
        var opts = new FracturedJsonOptions() { MaxInlineComplexity = 0 };

        var formatter = new Formatter() { Options = opts };

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var outputInvariant = formatter.Reformat(input, 0);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nb_NO");
            var outputNbNo = formatter.Reformat(input, 0);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr_FR");
            var outputFrFr = formatter.Reformat(input, 0);

            Assert.AreEqual(outputInvariant, outputNbNo);
            Assert.AreEqual(outputInvariant, outputFrFr);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
