using System.Diagnostics.CodeAnalysis;
using FracturedJson.Tokenizer;

namespace Tests;

[TestClass]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public class TokenizerTests
{
    [DataTestMethod]
    [DataRow("{", TokenType.BeginObject)]
    [DataRow("}", TokenType.EndObject)]
    [DataRow("[", TokenType.BeginArray)]
    [DataRow("]", TokenType.EndArray)]
    [DataRow(":", TokenType.Colon)]
    [DataRow(",", TokenType.Comma)]
    [DataRow("true", TokenType.True)]
    [DataRow("false", TokenType.False)]
    [DataRow("null", TokenType.Null)]
    [DataRow(@"""simple""", TokenType.String)]
    [DataRow(@"""with \t escapes\u80fE\r\n""", TokenType.String)]
    [DataRow(@"""""", TokenType.String)]
    [DataRow("3", TokenType.Number)]
    [DataRow("3.0", TokenType.Number)]
    [DataRow("-3", TokenType.Number)]
    [DataRow("-3.0", TokenType.Number)]
    [DataRow("0", TokenType.Number)]
    [DataRow("-0", TokenType.Number)]
    [DataRow("0.0", TokenType.Number)]
    [DataRow("9000", TokenType.Number)]
    [DataRow("3e2", TokenType.Number)]
    [DataRow("3.01e+2", TokenType.Number)]
    [DataRow("3e-2", TokenType.Number)]
    [DataRow("-3.01E-2", TokenType.Number)]
    [DataRow("\n", TokenType.BlankLine)]
    [DataRow("//\n", TokenType.LineComment)]
    [DataRow("// comment\n", TokenType.LineComment)]
    [DataRow("// comment", TokenType.LineComment)]
    [DataRow("/**/", TokenType.BlockComment)]
    [DataRow("/* comment */", TokenType.BlockComment)]
    [DataRow("/* comment\n *with* newline */", TokenType.BlockComment)]
    public void EchoesSingleToken(string input, TokenType type)
    {
        // The only case where we don't expect an exact match: a line comment token won't include the terminal \n
        var possiblyTrimmedInput = (type == TokenType.LineComment) ? input.TrimEnd() : input;
        
        var results = TokenScanner.Scan(input).ToArray();
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(possiblyTrimmedInput, results[0].Text);
        Assert.AreEqual(type, results[0].Type);
    }

    [DataTestMethod]
    [DataRow("{,", 1, 0, 1)]
    [DataRow("null,", 4, 0, 4)]
    [DataRow("3,", 1, 0, 1)]
    [DataRow("3.12,", 4, 0, 4)]
    [DataRow("3e2,", 3, 0, 3)]
    [DataRow(@"""st"",", 4, 0, 4)]
    [DataRow("null ,", 5, 0, 5)]
    [DataRow("null\t,", 5, 0, 5)]
    [DataRow("null\n,", 5, 1, 0)]
    [DataRow(" null \r\n ,", 9, 1, 1)]
    [DataRow("//co\n,", 5, 1, 0)]
    [DataRow("/**/,", 4, 0, 4)]
    [DataRow("/*1*/,", 5, 0, 5)]
    [DataRow("/*1\n*/,", 6, 1, 2)]
    [DataRow("\n\n", 1, 1, 0)]
    public void CorrectPositionForSecondToken(string input, long index, long row, long column)
    {
        var results = TokenScanner.Scan(input).ToArray();
        Assert.AreEqual(2, results.Length);
        Assert.AreEqual(index, results[1].InputPosition.Index);
        Assert.AreEqual(row, results[1].InputPosition.Row);
        Assert.AreEqual(column, results[1].InputPosition.Column);
    }

    [DataTestMethod]
    [DataRow("t")]
    [DataRow("nul")]
    [DataRow("/")]
    [DataRow("/*")]
    [DataRow("/* comment *")]
    [DataRow("\"")]
    [DataRow("\"string")]
    [DataRow(@"""string with escaped quote\""")]
    [DataRow("1.")]
    [DataRow("-")]
    [DataRow("1.0e")]
    [DataRow("1.0e+")]
    public void ThrowIfUnexpectedEnd(string input)
    {
        try
        {
            var _ = TokenScanner.Scan(input).ToArray();
        }
        catch (FracturedJsonException e)
        {
            Assert.IsTrue(e.InputPosition.HasValue);
            Assert.AreEqual(input.Length, e.InputPosition.Value.Index);
            return;
        }
        
        Assert.Fail("Expected an exception");
    }

    [DataTestMethod]
    [DataRow("/g,", 1, 0, 1)]
    [DataRow("nulg,", 3, 0, 3)]
    [DataRow(@"""\q"",", 2, 0, 2)]
    [DataRow(@"""\u111g"",", 6, 0, 6)]
    [DataRow("1.g", 2, 0, 2)]
    [DataRow("-g", 1, 0, 1)]
    [DataRow("1.0eg", 4, 0, 4)]
    [DataRow("\"beep\u0007beep\"", 5, 0, 5)]
    public void ThrowIfBadCharacter(string input, long index, long row, long column)
    {
        try
        {
            var _ = TokenScanner.Scan(input).ToArray();
        }
        catch (FracturedJsonException e)
        {
            Assert.IsTrue(e.InputPosition.HasValue);
            Assert.AreEqual(index, e.InputPosition.Value.Index);
            Assert.AreEqual(row, e.InputPosition.Value.Row);
            Assert.AreEqual(column, e.InputPosition.Value.Column);
            return;
        }
        
        Assert.Fail("Expected an exception");
    }

    [TestMethod]
    public void TokenSequenceMatchesSample()
    {
        // Keep each row 28 characters (plus 2 for eol) to make it easy to figure the expected index.  Note that the
        // \" sequences are C# escapes, so they show up as a single character in the input text.
        var inputRows = new[]
        {
            "{                           ",                            
            "    // A line comment       ",        
            "    \"item1\": \"a string\",    ",
            "                            ",
            "    /* a block              ",
            "       comment */           ",
            "    \"item2\": [null, -2.0]   ",
            "}                           "
        };
        var inputString = string.Join("\r\n", inputRows);
        var blockCommentText = inputRows[4].TrimStart() + "\r\n" + inputRows[5].TrimEnd();

        var expectedTokens = new[]
        {
            new JsonToken(TokenType.BeginObject, "{", new InputPosition(0, 0, 0)),
            new JsonToken(TokenType.LineComment, "// A line comment", new InputPosition(34, 1, 4)),
            new JsonToken(TokenType.String, "\"item1\"", new InputPosition(64, 2, 4)),
            new JsonToken(TokenType.Colon, ":", new InputPosition(71, 2, 11)),
            new JsonToken(TokenType.String, "\"a string\"", new InputPosition(73, 2, 13)),
            new JsonToken(TokenType.Comma, ",", new InputPosition(83, 2, 23)),
            new JsonToken(TokenType.BlankLine, "\n", new InputPosition(90, 3, 0)),
            new JsonToken(TokenType.BlockComment, blockCommentText, new InputPosition(124, 4, 4)),
            new JsonToken(TokenType.String, "\"item2\"", new InputPosition(184, 6, 4)),
            new JsonToken(TokenType.Colon, ":", new InputPosition(191, 6, 11)),
            new JsonToken(TokenType.BeginArray, "[", new InputPosition(193, 6, 13)),
            new JsonToken(TokenType.Null, "null", new InputPosition(194, 6, 14)),
            new JsonToken(TokenType.Comma, ",", new InputPosition(198, 6, 18)),
            new JsonToken(TokenType.Number, "-2.0", new InputPosition(200, 6, 20)),
            new JsonToken(TokenType.EndArray, "]", new InputPosition(204, 6, 24)),
            new JsonToken(TokenType.EndObject, "}", new InputPosition(210, 7, 0)),
        };
        
        var results = TokenScanner.Scan(inputString).ToArray();
        CollectionAssert.AreEqual(expectedTokens, results);
    }

    [TestMethod]
    public void EmptyInputHandled()
    {
        var results = TokenScanner.Scan(string.Empty).ToArray();
        Assert.AreEqual(0, results.Length);
    }

    [TestMethod]
    public void TestTokensFromFile()
    {
        var fileInfo = new FileInfo(Path.Combine("StandardJsonFiles", "1.json"));
        var results = TokenScanner.Scan(fileInfo).ToArray();
        Assert.IsTrue(results.Length > 100);
    }
}
