using FracturedJson;

namespace Tests;

[TestClass]
public class V3FormatterTests
{

    [TestMethod]
    public void GrossMisuseOfTestTools()
    {
        const string input = "[4.7, true, null, \"a string\", {}, false, []]";
        
        var opts = new FracturedJsonOptions()
        {
            MaxInlineLength = 80,
            MaxInlineComplexity = 0,
        };
        var formatter = new Formatter() { Options = opts };
        var output = formatter.Reformat(input, 0);
        
        Console.WriteLine(output);
    }
}
