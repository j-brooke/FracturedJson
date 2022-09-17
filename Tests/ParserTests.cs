using FracturedJson.V3;

namespace Tests;

[TestClass]
public class ParserTests
{
    [TestMethod]
    public void TestSimpleAndValidArray()
    {
        const string input = "[4.7, true, null, \"a string\", {}, false, []]";
        var parser = new Parser();
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(JsonItemType.Array ,docModel[0].Type);

        var expectedChildTypes = new[] { JsonItemType.Number, JsonItemType.True, JsonItemType.Null, JsonItemType.String, 
            JsonItemType.Object, JsonItemType.False, JsonItemType.Array };
        var foundChildTypes = docModel[0].Children.Select(ch => ch.Type).ToArray();
        CollectionAssert.AreEqual(expectedChildTypes, foundChildTypes);

        var expectedText = new[] { "4.7", "true", "null", "\"a string\"", null, "false", null };
        var foundText = docModel[0].Children.Select(ch => ch.Value).ToArray();
        CollectionAssert.AreEqual(expectedText, foundText);
    }

    [TestMethod]
    public void ArrayWithInlineBlockComments()
    {
        const string input = "[ /*a*/ 1 /*b*/ ]";

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(1, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[0].PostfixComment);
    }
    
    
    [TestMethod]
    public void ArrayWithMixedInlineComments()
    {
        var inputSegments = new[]
        {
            "[ /*a*/ 1 //b",
            "]",
        };
        var input = string.Join("\r\n", inputSegments);

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(1, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("//b", docModel[0].Children[0].PostfixComment);
    }
    
    [TestMethod]
    public void ArrayAmbiguousCommentFollowsComma1()
    {
        // Comment b could belong to either element 1 or 2.  Since it's on 1's side of the comment, put it there.
        const string input = "[ /*a*/ 1 /*b*/, 2 /*c*/ ]";

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[0].PostfixComment);
        Assert.IsNull(docModel[0].Children[1].PrefixComment);
        Assert.AreEqual("/*c*/", docModel[0].Children[1].PostfixComment);
    }
    
    [TestMethod]
    public void ArrayAmbiguousCommentFollowsComma2()
    {
        // Comment b could belong to either element 1 or 2.  Since it's on 2's side of the comment, put it there.
        const string input = "[ /*a*/ 1, /*b*/ 2 /*c*/ ]";

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.IsNull(docModel[0].Children[0].PostfixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[1].PrefixComment);
        Assert.AreEqual("/*c*/", docModel[0].Children[1].PostfixComment);
    }
    
    [TestMethod]
    public void ArrayAmbiguousCommentFollowsComma3()
    {
        // Comment b could belong to either element 1 or 2. It's after 1's comma, but on the same line with it,
        // so it belongs with 1.
        var inputSegments = new[]
        {
            "[ /*a*/ 1, /*b*/",
            "2 /*c*/ ]",
        };
        var input = string.Join("\r\n", inputSegments);

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[0].PostfixComment);
        Assert.IsNull(docModel[0].Children[1].PrefixComment);
        Assert.AreEqual("/*c*/", docModel[0].Children[1].PostfixComment);
    }
    
    [TestMethod]
    public void TestSimpleAndValidObject()
    {
        const string input = 
            "{ \"a\": 5.2, \"b\": false, \"c\": null, \"d\": true, \"e\":[], \"f\":{}, \"g\": \"a string\" }";
        var parser = new Parser();
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(JsonItemType.Object ,docModel[0].Type);

        var expectedChildTypes = new[] { JsonItemType.Number, JsonItemType.False, JsonItemType.Null, JsonItemType.True, 
            JsonItemType.Array, JsonItemType.Object, JsonItemType.String };
        var foundChildTypes = docModel[0].Children.Select(ch => ch.Type).ToArray();
        CollectionAssert.AreEqual(expectedChildTypes, foundChildTypes);

        var expectedPropNames = new[] { "\"a\"", "\"b\"", "\"c\"", "\"d\"", "\"e\"", "\"f\"", "\"g\"" };
        var foundPropNames = docModel[0].Children.Select(ch => ch.Name).ToArray();
        CollectionAssert.AreEqual(expectedPropNames, foundPropNames);
        
        var expectedText = new[] { "5.2", "false", "null", "true", null, null,  "\"a string\"" };
        var foundText = docModel[0].Children.Select(ch => ch.Value).ToArray();
        CollectionAssert.AreEqual(expectedText, foundText);
    }
    
    [TestMethod]
    public void ObjectWithInlineBlockComments()
    {
        const string input = "{ /*a*/ \"w\": /*b*/ 1 /*c*/ }";

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(1, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[0].MiddleComment);
        Assert.AreEqual("/*c*/", docModel[0].Children[0].PostfixComment);
    }
}

