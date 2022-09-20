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
        // Test of the most basic parsing functionality.
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
        // An object containing a single element, with comments that should be attached to it in all 3 positions.
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
    
    [TestMethod]
    public void ObjectMiddleCommentsCombined1()
    {
        // If there's more than one comment between a property name and value, we just merge them.  I suspect this will
        // never actually happen outside of unit tests. 
        var inputSegments = new[]
        {
            "{",
            "    \"w\" /*a*/ : //b",
            "        10.9,",
            "}"
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
        Assert.IsNull(docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*a*/ //b", docModel[0].Children[0].MiddleComment);
        Assert.IsNull(docModel[0].Children[0].PostfixComment);
    }
    
    [TestMethod]
    public void ObjectMiddleCommentsCombined2()
    {
        // If there's more than one comment between a property name and value, we just merge them.  I suspect this will
        // never actually happen outside of unit tests. 
        var inputSegments = new[]
        {
            "{",
            "    \"w\" /*a*/ :",
            "    /*b*/ 10.9,",
            "}"
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
        Assert.IsNull(docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*a*/ /*b*/", docModel[0].Children[0].MiddleComment);
        Assert.IsNull(docModel[0].Children[0].PostfixComment);
    }
    
    [TestMethod]
    public void ObjectMiddleCommentsCombined3()
    {
        // In this case we've got a line-ending comment and then a block comment, both between the property name
        // and its value.  Totally, totally plausible!  In this case, Parser squashes them into a single middle comment,
        // but with a newline in there.
        var inputSegments = new[]
        {
            "{",
            "    \"w\": //a",
            "    /*b*/ 10.9,",
            "}"
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
        Assert.IsNull(docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("//a\n/*b*/", docModel[0].Children[0].MiddleComment);
        Assert.IsNull(docModel[0].Children[0].PostfixComment);
    }
    
    [TestMethod]
    public void ObjectCommentsPreferSameLineElements()
    {
        // All of the comments here should be attached to the element on their same line.
        var inputSegments = new[]
        {
            "{",
            "          \"w\": 1, /*a*/",
            "    /*b*/ \"x\": 2, /*c*/",
            "          \"y\": 3  /*d*/",
            "}"
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
        Assert.AreEqual(3, docModel[0].Children.Count);
        Assert.IsNull(docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PostfixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[1].PrefixComment);
        Assert.AreEqual("/*c*/", docModel[0].Children[1].PostfixComment);
        Assert.IsNull(docModel[0].Children[2].PrefixComment);
        Assert.AreEqual("/*d*/", docModel[0].Children[2].PostfixComment);
    }

    
    [TestMethod]
    public void ObjectWithInlineBlockComments2()
    {
        // Here, comment a should be postfix-attached to w:1.
        const string input = "{  \"w\": 1, /*a*/ \"x\": 2 }";

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
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PostfixComment);
    }
    
    [TestMethod]
    public void ObjectWithInlineBlockComments3()
    {
        // Not the ideal behavior, but I'm codifying it in this test.  In the case below, to a human eye, it would
        // probably make sense to postfix comment a onto element w, and prefix b onto x.  But the current logic doesn't
        // do that, and I don't think this is a case worth all sorts of special code for.
        // 
        // So actually happens (and what we're testing for) is that comment a gets postfixed to w, but then comment
        // b gets treated as a stand-alone comment.
        const string input = "{  \"w\": 1, /*a*/ /*b*/ \"x\": 2 }";

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(3, docModel[0].Children.Count);
        
        Assert.AreEqual("\"w\"", docModel[0].Children[0].Name);
        Assert.AreEqual(JsonItemType.Number, docModel[0].Children[0].Type);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PostfixComment);
        
        Assert.IsNull(docModel[0].Children[1].Name);
        Assert.AreEqual(JsonItemType.BlockComment, docModel[0].Children[1].Type);
        Assert.AreEqual("/*b*/", docModel[0].Children[1].Value);
        
        Assert.AreEqual("\"x\"", docModel[0].Children[2].Name);
        Assert.AreEqual(JsonItemType.Number, docModel[0].Children[2].Type);
        Assert.IsNull(docModel[0].Children[2].PrefixComment);
    }

    [TestMethod]
    public void ArrayCommentsForMultilineElement()
    {
        // Comments that should be attached to a multi-line array.
        var inputSegments = new[]
        {
            "[",
            "    /*a*/ [",
            "        1, 2, 3",
            "    ] //b",
            "]"
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
    public void ObjectCommentsForMultilineElement()
    {
        // Comments that should be attached to a multi-line array.
        var inputSegments = new[]
        {
            "{",
            "    /*a*/ \"w\": [",
            "        1, 2, 3",
            "    ] //b",
            "}"
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
    public void DepthAndComplexityWork()
    {
        var inputSegments = new[]
        {
            "[",
            "    null,",
            "    [ 1, 2, 3 ],",
            "    [ 1, 2, {}],",
            "    [ 1, 2, { /*a*/ }],",
            "    [ 1, 2, { \"w\": 1 }]",
            "]"
        };
        var input = string.Join("\r\n", inputSegments);
        var parser = new Parser();
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();

        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(0, docModel[0].Depth);
        Assert.AreEqual(3, docModel[0].Complexity);
        Assert.AreEqual(5, docModel[0].Children.Count);
        
        // Primitive elements always have a complexity of 0.
        Assert.AreEqual(1, docModel[0].Children[0].Depth);
        Assert.AreEqual(0, docModel[0].Children[0].Complexity);
        
        // An array/object always has a complexity 1 greater than the greatest of its child element complexities.
        Assert.AreEqual(1, docModel[0].Children[1].Depth);
        Assert.AreEqual(1, docModel[0].Children[1].Complexity);

        // An empty array/object has a complexity of 0, so this is treated the same as the case above.
        Assert.AreEqual(1, docModel[0].Children[2].Depth);
        Assert.AreEqual(1, docModel[0].Children[2].Complexity);
        Assert.AreEqual(2, docModel[0].Children[2].Children[2].Depth);
        Assert.AreEqual(0, docModel[0].Children[2].Children[2].Complexity);

        // Comments don't count when determining an object/array's complexity, so this is the same as above.
        Assert.AreEqual(1, docModel[0].Children[3].Depth);
        Assert.AreEqual(1, docModel[0].Children[3].Complexity);
        Assert.AreEqual(2, docModel[0].Children[3].Children[2].Depth);
        Assert.AreEqual(0, docModel[0].Children[3].Children[2].Complexity);

        // Here there's a non-empty object nested in the array, so it's more complex.
        Assert.AreEqual(1, docModel[0].Children[4].Depth);
        Assert.AreEqual(2, docModel[0].Children[4].Complexity);
        Assert.AreEqual(2, docModel[0].Children[4].Children[2].Depth);
        Assert.AreEqual(1, docModel[0].Children[4].Children[2].Complexity);
    }

    /// <summary>
    /// In general, our computed MinimumTotalLength should be equal to the length of a System.Text.Json minified string.
    /// (This depends on a few things, like that there are no comments, and that StringLengthFunc is equivalent to
    /// String.Length.
    /// </summary>
    [DataTestMethod]
    [DataRow("3.13")]
    [DataRow("[]")]
    [DataRow("[3, 7, 9.2]")]
    [DataRow("[\n\n 3,\n 7,\n 9.2\n\n]")]
    [DataRow("[3, 7, 9.2, {}]")]
    [DataRow("[3, 7, 9.2, { \"w\": -1, \"x\": null}]")]
    public void MinimumLengthSameAsMinifiedString(string input)
    {
        var parser = new Parser();
        var docModel = parser.ParseTopLevel(input, 0, false).ToArray();

        var dotnetDocModel = System.Text.Json.Nodes.JsonNode.Parse(input);
        var dotnetJsonString = dotnetDocModel!.ToJsonString();

        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(dotnetJsonString.Length, docModel[0].MinimumTotalLength);
    }
}

