using FracturedJson;
using FracturedJson.Parsing;
using FracturedJson.Tokenizing;

namespace Tests;

[TestClass]
public class ParserTests
{
    [TestMethod]
    public void TestSimpleAndValidArray()
    {
        const string input = "[4.7, true, null, \"a string\", {}, false, []]";
        var parser = new Parser();
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(JsonItemType.Array ,docModel[0].Type);

        var expectedChildTypes = new[] { JsonItemType.Number, JsonItemType.True, JsonItemType.Null, JsonItemType.String, 
            JsonItemType.Object, JsonItemType.False, JsonItemType.Array };
        var foundChildTypes = docModel[0].Children.Select(ch => ch.Type).ToArray();
        CollectionAssert.AreEqual(expectedChildTypes, foundChildTypes);

        var expectedText = new[] { "4.7", "true", "null", "\"a string\"", string.Empty, "false", string.Empty };
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(1, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("//b", docModel[0].Children[0].PostfixComment);
    }

    [TestMethod]
    public void ArrayWithUnattachedTrailingComment()
    {
        var inputSegments = new[]
        {
            "[ /*a*/ 1 /*b*/ /*c*/",
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        Assert.AreEqual(JsonItemType.Number, docModel[0].Children[0].Type);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[0].PostfixComment);
        Assert.AreEqual(JsonItemType.BlockComment, docModel[0].Children[1].Type);
        Assert.AreEqual("/*c*/", docModel[0].Children[1].Value);
    }

    [TestMethod]
    public void ArrayWithMultipleLeadingComments()
    {
        var input = "[ /*a*/ /*b*/ 1 ]";

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        Assert.AreEqual(JsonItemType.BlockComment, docModel[0].Children[0].Type);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].Value);
        Assert.AreEqual(JsonItemType.Number, docModel[0].Children[1].Type);
        Assert.AreEqual("/*b*/", docModel[0].Children[1].PrefixComment);
    }

    [TestMethod]
    public void ArrayAmbiguousCommentPrecedesComma()
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[0].PostfixComment);
        Assert.AreEqual(0, docModel[0].Children[1].PrefixCommentLength);
        Assert.AreEqual("/*c*/", docModel[0].Children[1].PostfixComment);
    }

    [TestMethod]
    public void ArrayAmbiguousCommentFollowsComma()
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual(0, docModel[0].Children[0].PostfixCommentLength);
        Assert.AreEqual("/*b*/", docModel[0].Children[1].PrefixComment);
        Assert.AreEqual("/*c*/", docModel[0].Children[1].PostfixComment);
    }

    [TestMethod]
    public void ArrayAmbiguousCommentFollowsCommaWithNewline()
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[0].PostfixComment);
        Assert.AreEqual(0, docModel[0].Children[1].PrefixCommentLength);
        Assert.AreEqual("/*c*/", docModel[0].Children[1].PostfixComment);
    }

    [TestMethod]
    public void ArrayMultipleUnattachedComments()
    {
        // The comments are on a separate line from the element, so they're unattached.
        var inputSegments = new[]
        {
            "[",
            "    /*a*/ //b",
            "    null",
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(3, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].Value);
        Assert.AreEqual("//b", docModel[0].Children[1].Value);
        Assert.AreEqual(JsonItemType.Null, docModel[0].Children[2].Type);
    }
    
    [TestMethod]
    public void ArrayMultilineCommentStandsAlone()
    {
        // Since the comment here is a multi-line block comment, it's a standalone comment, not attached to either 
        // element.
        var inputSegments = new[]
        {
            "[",
            "    1, /*a",
            "    b*/ 2",
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(3, docModel[0].Children.Count);
        Assert.AreEqual("1", docModel[0].Children[0].Value);
        Assert.AreEqual("/*a\r\n    b*/", docModel[0].Children[1].Value);
        Assert.AreEqual("2", docModel[0].Children[2].Value);
    }
    
    [TestMethod]
    public void ArrayBlankLinesArePreservedOrRemoved()
    {
        var inputSegments = new[]
        {
            "[",
            "",
            "    //comment",
            "    true,",
            "",
            "    ",
            "    false",
            "]",
        };
        var input = string.Join("\r\n", inputSegments);

        // First try the permissive options
        var preserveOptions = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var preserveParser = new Parser() { Options = preserveOptions };
        var preserveDocModel = preserveParser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, preserveDocModel.Length);
        Assert.AreEqual(JsonItemType.Array ,preserveDocModel[0].Type);

        var preserveExpectedTypes = new[] { JsonItemType.BlankLine, JsonItemType.LineComment, JsonItemType.True,
            JsonItemType.BlankLine, JsonItemType.BlankLine, JsonItemType.False };
        var preserveFoundTypes = preserveDocModel[0].Children.Select(ch => ch.Type).ToArray();
        CollectionAssert.AreEqual(preserveExpectedTypes, preserveFoundTypes);
        
        // Now turn that stuff off
        var removeOptions = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Remove, 
            AllowTrailingCommas = true,
            PreserveBlankLines = false,
        };
        var removeParser = new Parser() { Options = removeOptions };
        var removeDocModel = removeParser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, removeDocModel.Length);
        Assert.AreEqual(JsonItemType.Array ,removeDocModel[0].Type);
        var removeExpectedTypes = new[] { JsonItemType.True, JsonItemType.False };
        var removeFoundTypes = removeDocModel[0].Children.Select(ch => ch.Type).ToArray();
        CollectionAssert.AreEqual(removeExpectedTypes, removeFoundTypes);
    }
    
    [TestMethod]
    public void TestSimpleAndValidObject()
    {
        // Test of the most basic parsing functionality.
        const string input = 
            "{ \"a\": 5.2, \"b\": false, \"c\": null, \"d\": true, \"e\":[], \"f\":{}, \"g\": \"a string\" }";
        var parser = new Parser();
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(JsonItemType.Object ,docModel[0].Type);

        var expectedChildTypes = new[] { JsonItemType.Number, JsonItemType.False, JsonItemType.Null, JsonItemType.True, 
            JsonItemType.Array, JsonItemType.Object, JsonItemType.String };
        var foundChildTypes = docModel[0].Children.Select(ch => ch.Type).ToArray();
        CollectionAssert.AreEqual(expectedChildTypes, foundChildTypes);

        var expectedPropNames = new[] { "\"a\"", "\"b\"", "\"c\"", "\"d\"", "\"e\"", "\"f\"", "\"g\"" };
        var foundPropNames = docModel[0].Children.Select(ch => ch.Name).ToArray();
        CollectionAssert.AreEqual(expectedPropNames, foundPropNames);
        
        var expectedText = new[] { "5.2", "false", "null", "true", string.Empty, string.Empty,  "\"a string\"" };
        var foundText = docModel[0].Children.Select(ch => ch.Value).ToArray();
        CollectionAssert.AreEqual(expectedText, foundText);
    }

    [TestMethod]
    public void ObjectBlankLinesArePreservedOrRemoved()
    {
        var inputSegments = new[]
        {
            "{",
            "",
            "    //comment",
            "    \"w\": true,",
            "",
            "    ",
            "    \"x\": false",
            "}",
        };
        var input = string.Join("\r\n", inputSegments);

        var preserveOptions = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var preserveParser = new Parser() { Options = preserveOptions };
        var preserveDocModel = preserveParser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, preserveDocModel.Length);
        Assert.AreEqual(JsonItemType.Object ,preserveDocModel[0].Type);

        var preserveExpectedTypes = new[] { JsonItemType.BlankLine, JsonItemType.LineComment, JsonItemType.True,
            JsonItemType.BlankLine, JsonItemType.BlankLine, JsonItemType.False };
        var preserveFoundTypes = preserveDocModel[0].Children.Select(ch => ch.Type).ToArray();
        CollectionAssert.AreEqual(preserveExpectedTypes, preserveFoundTypes);
        
        // Now turn that stuff off
        var removeOptions = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Remove, 
            AllowTrailingCommas = true,
            PreserveBlankLines = false,
        };
        var removeParser = new Parser() { Options = removeOptions };
        var removeDocModel = removeParser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, removeDocModel.Length);
        Assert.AreEqual(JsonItemType.Object ,removeDocModel[0].Type);
        var removeExpectedTypes = new[] { JsonItemType.True, JsonItemType.False };
        var removeFoundTypes = removeDocModel[0].Children.Select(ch => ch.Type).ToArray();
        CollectionAssert.AreEqual(removeExpectedTypes, removeFoundTypes);
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(1, docModel[0].Children.Count);
        Assert.AreEqual(0, docModel[0].Children[0].PrefixCommentLength);
        Assert.AreEqual("/*a*/\n//b\n", docModel[0].Children[0].MiddleComment);
        Assert.AreEqual(0, docModel[0].Children[0].PostfixCommentLength);
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(1, docModel[0].Children.Count);
        Assert.AreEqual(string.Empty,docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("/*a*/\n/*b*/", docModel[0].Children[0].MiddleComment);
        Assert.AreEqual(string.Empty, docModel[0].Children[0].PostfixComment);
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(1, docModel[0].Children.Count);
        Assert.AreEqual(string.Empty, docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("//a\n/*b*/", docModel[0].Children[0].MiddleComment);
        Assert.AreEqual(string.Empty, docModel[0].Children[0].PostfixComment);
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
            "          \"y\": 3,  /*d*/",
            "          \"z\": 4",
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(4, docModel[0].Children.Count);
        Assert.AreEqual(0, docModel[0].Children[0].PrefixCommentLength);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PostfixComment);
        Assert.AreEqual("/*b*/", docModel[0].Children[1].PrefixComment);
        Assert.AreEqual("/*c*/", docModel[0].Children[1].PostfixComment);
        Assert.AreEqual(0, docModel[0].Children[2].PrefixCommentLength);
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PostfixComment);
    }

    [TestMethod]
    public void ObjectWithInlineBlockComments3()
    {
        // Here, we want comment a to be post-fixed to "w":1 and b to be prefixed to "x":2.
        const string input = "{  \"w\": 1, /*a*/ /*b*/ \"x\": 2 }";

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(2, docModel[0].Children.Count);
        
        Assert.AreEqual("\"w\"", docModel[0].Children[0].Name);
        Assert.AreEqual(JsonItemType.Number, docModel[0].Children[0].Type);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PostfixComment);
        
        Assert.AreEqual("\"x\"", docModel[0].Children[1].Name);
        Assert.AreEqual(JsonItemType.Number, docModel[0].Children[1].Type);
        Assert.AreEqual("/*b*/", docModel[0].Children[1].PrefixComment);
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
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
        var docModel = parser.ParseTopLevel(input, false).ToArray();
        
        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(1, docModel[0].Children.Count);
        Assert.AreEqual("/*a*/", docModel[0].Children[0].PrefixComment);
        Assert.AreEqual("//b", docModel[0].Children[0].PostfixComment);
    }

    [TestMethod]
    public void ComplexityWork()
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

        var options = new FracturedJsonOptions()
        { 
            CommentPolicy = CommentPolicy.Preserve, 
            AllowTrailingCommas = true,
            PreserveBlankLines = true,
        };
        var parser = new Parser() { Options = options };
        var docModel = parser.ParseTopLevel(input, false).ToArray();

        Assert.AreEqual(1, docModel.Length);
        Assert.AreEqual(3, docModel[0].Complexity);
        Assert.AreEqual(5, docModel[0].Children.Count);
        
        // Primitive elements always have a complexity of 0.
        Assert.AreEqual(0, docModel[0].Children[0].Complexity);
        
        // An array/object always has a complexity 1 greater than the greatest of its child element complexities.
        Assert.AreEqual(1, docModel[0].Children[1].Complexity);

        // An empty array/object has a complexity of 0, so this is treated the same as the case above.
        Assert.AreEqual(1, docModel[0].Children[2].Complexity);
        Assert.AreEqual(0, docModel[0].Children[2].Children[2].Complexity);

        // Comments don't count when determining an object/array's complexity, so this is the same as above.
        Assert.AreEqual(1, docModel[0].Children[3].Complexity);
        Assert.AreEqual(0, docModel[0].Children[3].Children[2].Complexity);

        // Here there's a non-empty object nested in the array, so it's more complex.
        Assert.AreEqual(2, docModel[0].Children[4].Complexity);
        Assert.AreEqual(1, docModel[0].Children[4].Children[2].Complexity);
    }

    [DataTestMethod]
    [DataRow("[,1]")]
    [DataRow("[1 2]")]
    [DataRow("[1, 2,]")]
    [DataRow("[1, 2}")]
    [DataRow("[1, 2")]
    [DataRow("[1, /*a*/ 2]")]
    [DataRow("[1, //a\n 2]")]
    [DataRow("{, \"w\":1 }")]
    [DataRow("{ \"w\":1 ")]
    [DataRow("{ /*a*/ \"w\":1 }")]
    [DataRow("{ \"w\":1, }")]
    [DataRow("{ \"w\":1 ]")]
    [DataRow("{ \"w\"::1 ")]
    [DataRow("{ \"w\" \"foo\" }")]
    [DataRow("{ \"w\" {:1 }")]
    [DataRow("{ \"w\":1 \"x\":2 }")]
    public void ThrowsForMalformedData(string input)
    {
        var parser = new Parser();
        Assert.ThrowsException<FracturedJsonException>(() => parser.ParseTopLevel(input, false).ToArray());
    }

    [TestMethod]
    public void StopsAfterFirstToken()
    {
        const string input = "[ 1, 2 ],[ 3, 4 ]";
        var parser = new Parser();
        var docModel = parser.ParseTopLevel(input, true).ToArray();

        Assert.AreEqual(1, docModel.Length);
    }
}

