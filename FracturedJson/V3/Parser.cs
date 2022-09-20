using System;
using System.Collections.Generic;
using System.Linq;
using FracturedJson.Tokenizer;

namespace FracturedJson.V3;

/// <summary>
/// Class that takes JSON input (possibly with comments) and converts it to a forrest of <see cref="JsonItem"/> objects
/// representing the data and its structure.  Great pains are taken to try to keep comments with the elements to
/// which they seem to refer.
/// </summary>
public class Parser
{
    public FracturedJsonOptions Options { get; set; } = new();
    public Func<string, int> StringLengthFunc = StringLengthByCharacterCount;

    /// <summary>
    /// Returns a sequence of <see cref="JsonItem"/>s representing the top-level items in the input.  In a typical
    /// JSON you're only allowed to have one top-level value, but since there might be comments or blank lines,
    /// we have to be able to return multiple things before or after the actual data.
    /// </summary>
    /// <param name="charEnumeration">The JSON (with comments, maybe) text</param>
    /// <param name="startingDepth">Starting logical depth, for use when formatting</param>
    /// <param name="stopAfterFirstElem">If true, the enumeration ends when a single top-level element (real JSON value)
    /// is read.  </param>
    /// <returns>JsonItems representing the top-level data, comments, and blank lines from the input.</returns>
    public IEnumerable<JsonItem> ParseTopLevel(IEnumerable<char> charEnumeration, int startingDepth,
        bool stopAfterFirstElem)
    {
        var tokenStream = TokenScanner.Scan(charEnumeration);
        return ParseTopLevel(tokenStream, startingDepth, stopAfterFirstElem);
    }

    private IEnumerable<JsonItem> ParseTopLevel(IEnumerable<JsonToken> tokenEnumeration, int startingDepth,
        bool stopAfterFirstElem)
    {
        using var enumerator = tokenEnumeration.GetEnumerator();
        while (true)
        {
            if (!enumerator.MoveNext())
                yield break;

            var item = ParseItem(enumerator, startingDepth);
            ComputeItemLengths(item);
            yield return item;
            var isElement = item.Type != JsonItemType.BlankLine && item.Type != JsonItemType.BlockComment &&
                            item.Type != JsonItemType.LineComment;
            if (isElement && stopAfterFirstElem)
                yield break;
        }
    }

    /// <summary>
    /// Parse a simple token (not an array or object) into a <see cref="JsonItem"/>.  The enumerator should be pointed
    /// at the token to be processed.  It won't be changed by this call.
    /// </summary>
    private JsonItem ParseSimple(IEnumerator<JsonToken> enumerator, int depth)
    {
        var token = enumerator.Current;
        var itemType = token.Type switch
        {
            TokenType.False => JsonItemType.False,
            TokenType.True => JsonItemType.True,
            TokenType.Null => JsonItemType.Null,
            TokenType.Number => JsonItemType.Number,
            TokenType.String => JsonItemType.String,
            TokenType.BlankLine => JsonItemType.BlankLine,
            TokenType.BlockComment => JsonItemType.BlockComment,
            TokenType.LineComment => JsonItemType.LineComment,
            _ => throw FracturedJsonException.Create("Unexpected token", token.InputPosition),
        };

        var item = new JsonItem
        {
            Type = itemType,
            Value = token.Text,
            InputLine = token.InputPosition.Row,
            Depth = depth,
            Complexity = 0,
        };

        return item;
    }

    /// <summary>
    /// Parse the stream of tokens into a JSON array (recursively).  The enumerator should be pointing to the open
    /// square bracket token at the start of the call.  It will be pointing to the closing bracket when the call
    /// returns.
    /// </summary>
    private JsonItem ParseArray(IEnumerator<JsonToken> enumerator, int depth)
    {
        if (enumerator.Current.Type != TokenType.BeginArray)
            throw FracturedJsonException.Create("Parser logic error", enumerator.Current.InputPosition);

        var startingInputPosition = enumerator.Current.InputPosition;

        // An element that was already added to the child list that is eligible for a postfix comment.
        JsonItem? elemNeedingPostComment = null;
        var elemNeedingPostEndRow = -1L;

        // A single-line block comment that HAS NOT been added to the child list, that might serve as a prefix comment.
        JsonItem? unplacedComment = null;

        var childList = new List<JsonItem>();
        var commaStatus = CommaStatus.EmptyCollection;
        var endOfArrayFound = false;
        var thisArrayComplexity = 0;
        while (!endOfArrayFound)
        {
            // Get the next token, or throw an error if the input ends.
            var token = GetNextTokenOrThrown(enumerator, startingInputPosition);

            // If the token we're about to deal with isn't on the same line as an unplaced comment or is the end of the
            // array, this is our last chance to find a place for that comment.
            var unplacedCommentNeedsHome = unplacedComment != null &&
                                           (unplacedComment.InputLine != token.InputPosition.Row ||
                                            token.Type == TokenType.EndArray);
            if (unplacedCommentNeedsHome)
            {
                if (elemNeedingPostComment != null)
                {
                    // So there's a comment we don't have a place for yet, and a previous element that doesn't have
                    // a postfix comment.  And since the new token is on a new line (or end of array), the comment
                    // doesn't belong to whatever is coming up next.  So attach the unplaced comment to the old 
                    // element.  (This is probably a comment at the end of a line after a comma.)
                    elemNeedingPostComment.PostfixComment = unplacedComment!.Value;
                    elemNeedingPostComment.IsPostCommentBlockStyle = unplacedComment.Type == JsonItemType.BlockComment;
                }
                else
                {
                    // There's no old element to attach it to, so just add the comment as a standalone child.
                    childList.Add(unplacedComment!);
                }

                unplacedComment = null;
            }

            // If the token we're about to deal with isn't on the same line as the last element, the new token obviously 
            // won't be a postfix comment.
            if (elemNeedingPostComment != null && elemNeedingPostEndRow != token.InputPosition.Row)
                elemNeedingPostComment = null;

            switch (token.Type)
            {
                case TokenType.EndArray:
                    if (commaStatus == CommaStatus.CommaSeen && !Options.AllowTrailingCommas)
                        throw FracturedJsonException.Create("Array may not end with a comma with current options",
                            token.InputPosition);
                    endOfArrayFound = true;
                    break;

                case TokenType.Comma:
                    if (commaStatus != CommaStatus.ElementSeen)
                        throw FracturedJsonException.Create("Unexpected comma in array", token.InputPosition);
                    commaStatus = CommaStatus.CommaSeen;
                    break;

                case TokenType.BlankLine:
                    if (!Options.PreserveBlankLines)
                        break;
                    childList.Add(ParseSimple(enumerator, depth + 1));
                    break;

                case TokenType.BlockComment:
                    if (Options.CommentPolicy == CommentPolicy.Remove)
                        break;
                    if (Options.CommentPolicy == CommentPolicy.TreatAsError)
                        throw FracturedJsonException.Create("Comments not allowed with current options",
                            token.InputPosition);

                    if (unplacedComment != null)
                    {
                        // There was a block comment before this one.  Add it as an unattached comment to make room.
                        childList.Add(unplacedComment);
                        unplacedComment = null;
                    }

                    // If this is a multiline comment, add it as unattached.
                    var commentItem = ParseSimple(enumerator, depth + 1);
                    if (IsMultilineComment(commentItem))
                    {
                        childList.Add(commentItem);
                        break;
                    }

                    // If this comment came after an element and before a comma, attach it to that element.
                    if (elemNeedingPostComment != null && commaStatus == CommaStatus.ElementSeen)
                    {
                        elemNeedingPostComment.PostfixComment = commentItem.Value;
                        elemNeedingPostComment.IsPostCommentBlockStyle = true;
                        elemNeedingPostComment = null;
                        break;
                    }

                    // Hold on to it for now.  Even if elemNeedingPostComment != null, it's possible that this comment
                    // should be attached to the next element, not that one.  (For instance, two elements on the same
                    // line, with a comment between them.)
                    unplacedComment = commentItem;
                    break;

                case TokenType.LineComment:
                    if (Options.CommentPolicy == CommentPolicy.Remove)
                        break;
                    if (Options.CommentPolicy == CommentPolicy.TreatAsError)
                        throw FracturedJsonException.Create("Comments not allowed with current options",
                            token.InputPosition);

                    if (unplacedComment != null)
                    {
                        // A previous comment followed by a line-ending comment?  Add them both as unattached comments
                        childList.Add(unplacedComment);
                        childList.Add(ParseSimple(enumerator, depth + 1));
                        unplacedComment = null;
                        break;
                    }

                    if (elemNeedingPostComment != null)
                    {
                        // Since this is a line comment, we know there isn't anything else on the line after this.
                        // So if there was an element before this that can take a comment, attach it.
                        elemNeedingPostComment.PostfixComment = token.Text;
                        elemNeedingPostComment.IsPostCommentBlockStyle = false;
                        elemNeedingPostComment = null;
                        break;
                    }

                    childList.Add(ParseSimple(enumerator, depth + 1));
                    break;

                case TokenType.False:
                case TokenType.True:
                case TokenType.Null:
                case TokenType.String:
                case TokenType.Number:
                case TokenType.BeginArray:
                case TokenType.BeginObject:
                    var element = ParseItem(enumerator, depth + 1);
                    commaStatus = CommaStatus.ElementSeen;
                    thisArrayComplexity = Math.Max(thisArrayComplexity, element.Complexity + 1);

                    if (unplacedComment != null)
                    {
                        element.PrefixComment = unplacedComment.Value;
                        unplacedComment = null;
                    }

                    childList.Add(element);
                    
                    // Remember this element and the row it ended on (not token.InputPosition.Row).
                    elemNeedingPostComment = element;
                    elemNeedingPostEndRow = enumerator.Current.InputPosition.Row;
                    break;

                default:
                    throw FracturedJsonException.Create("Unexpected token in array", token.InputPosition);
            }
        }

        foreach (var item in childList)
            ComputeItemLengths(item);

        var arrayItem = new JsonItem()
        {
            Type = JsonItemType.Array,
            InputLine = startingInputPosition.Row,
            Depth = depth,
            Complexity = thisArrayComplexity,
            Children = childList,
        };

        return arrayItem;
    }

    /// <summary>
    /// Parse the stream of tokens into a JSON object (recursively).  The enumerator should be pointing to the open
    /// curly bracket token at the start of the call.  It will be pointing to the closing bracket when the call
    /// returns.
    /// </summary>
    private JsonItem ParseObject(IEnumerator<JsonToken> enumerator, int depth)
    {
        if (enumerator.Current.Type != TokenType.BeginObject)
            throw FracturedJsonException.Create("Parser logic error", enumerator.Current.InputPosition);

        var startingInputPosition = enumerator.Current.InputPosition;

        var childList = new List<JsonItem>();

        // Variables to collect the pieces as we go.  We'll put them all together and add them to the child list 
        // when conditions are appropriate.
        JsonToken? propertyName = null;
        JsonItem? propertyValue = null;
        long linePropValueEnds = -1;
        var beforePropComments = new List<JsonItem>();
        var midPropComments = new List<JsonToken>();
        var afterPropComments = new List<JsonItem>();
        
        var phase = ObjectPhase.BeforePropName;
        var thisObjComplexity = 0;
        var endOfObject = false;
        while (!endOfObject)
        {
            var token = GetNextTokenOrThrown(enumerator, startingInputPosition);

            var isNewLine = (linePropValueEnds != token.InputPosition.Row);
            var isEndOfObject = (token.Type == TokenType.EndObject);
            var startingNextPropName = (token.Type == TokenType.String && phase == ObjectPhase.AfterComma);
            var needToFlush = propertyName != null && propertyValue != null &&
                              (isNewLine || isEndOfObject || startingNextPropName);
            if (needToFlush)
            {
                AttachObjectValuePieces(childList, propertyName!.Value, propertyValue!, linePropValueEnds,
                    beforePropComments, midPropComments, afterPropComments);
                thisObjComplexity = Math.Max(thisObjComplexity, propertyValue!.Complexity + 1);
                propertyName = null;
                propertyValue = null;
                if (!isEndOfObject)
                    phase = ObjectPhase.BeforePropName;
            }

            switch (token.Type)
            {
                case TokenType.BlankLine:
                case TokenType.BlockComment:
                case TokenType.LineComment:
                    if (phase == ObjectPhase.BeforePropName)
                        beforePropComments.Add(ParseSimple(enumerator, depth + 1));
                    else if (phase == ObjectPhase.AfterPropName || phase == ObjectPhase.AfterColon)
                        midPropComments.Add(token);
                    else
                        afterPropComments.Add(ParseSimple(enumerator, depth + 1));
                    break;
                case TokenType.EndObject:
                    endOfObject = true;
                    break;
                case TokenType.String:
                    if (phase == ObjectPhase.BeforePropName || phase == ObjectPhase.AfterComma)
                    {
                        propertyName = token;
                        phase = ObjectPhase.AfterPropName;
                    }
                    else if (phase == ObjectPhase.AfterColon)
                    {
                        propertyValue = ParseItem(enumerator, depth + 1);
                        linePropValueEnds = enumerator.Current.InputPosition.Row;
                        phase = ObjectPhase.AfterPropValue;
                    }
                    else
                        throw FracturedJsonException.Create("Unexpected string found while processing object",
                            token.InputPosition);

                    break;
                case TokenType.False:
                case TokenType.True:
                case TokenType.Null:
                case TokenType.Number:
                case TokenType.BeginArray:
                case TokenType.BeginObject:
                    if (phase != ObjectPhase.AfterColon)
                        throw FracturedJsonException.Create("Unexpected element while processing object",
                            token.InputPosition);
                    propertyValue = ParseItem(enumerator, depth + 1);
                    linePropValueEnds = enumerator.Current.InputPosition.Row;
                    phase = ObjectPhase.AfterPropValue;
                    break;
                case TokenType.Colon:
                    if (phase != ObjectPhase.AfterPropName)
                        throw FracturedJsonException.Create("Unexpected colon while processing object",
                            token.InputPosition);
                    phase = ObjectPhase.AfterColon;
                    break;
                case TokenType.Comma:
                    if (phase != ObjectPhase.AfterPropValue)
                        throw FracturedJsonException.Create("Unexpected comma while processing object",
                            token.InputPosition);
                    phase = ObjectPhase.AfterComma;
                    break;
                default:
                    throw FracturedJsonException.Create("Unexpected token while processing object",
                        token.InputPosition);
            }
        }

        if (!Options.AllowTrailingCommas && phase == ObjectPhase.AfterComma)
            throw FracturedJsonException.Create("Object may not end with comma with current options",
                enumerator.Current.InputPosition);

        foreach (var item in childList)
            ComputeItemLengths(item);

        var objItem = new JsonItem()
        {
            Type = JsonItemType.Object,
            InputLine = startingInputPosition.Row,
            Depth = depth,
            Complexity = thisObjComplexity,
            Children = childList,
        };
        return objItem;
    }

    private JsonItem ParseItem(IEnumerator<JsonToken> enumerator, int depth)
    {
        return enumerator.Current.Type switch
        {
            TokenType.BeginArray => ParseArray(enumerator, depth),
            TokenType.BeginObject => ParseObject(enumerator, depth),
            _ => ParseSimple(enumerator, depth),
        };
    }

    private void ComputeItemLengths(JsonItem item)
    {
        const char newLineChar = '\n';
        
        item.NameLength = StringLengthWithNullCheck(item.Name);
        item.ValueLength = StringLengthWithNullCheck(item.Value);
        item.PrefixCommentLength = StringLengthWithNullCheck(item.PrefixComment);
        item.MiddleCommentLength = StringLengthWithNullCheck(item.MiddleComment);
        item.PostfixCommentLength = StringLengthWithNullCheck(item.PostfixComment);

        item.CommentsIncludeLineBreaks = (item.PrefixComment != null && item.PrefixComment.Contains(newLineChar))
                                         || (item.MiddleComment != null && item.MiddleComment.Contains(newLineChar))
                                         || (item.PostfixComment != null && item.PostfixComment.Contains(newLineChar));

        var bracketLength = (item.Type == JsonItemType.Array || item.Type == JsonItemType.Object) ? 2 : 0;
        var totalMinimumLength = 0L
                                 + item.NameLength
                                 + item.ValueLength
                                 + item.PrefixCommentLength
                                 + item.MiddleCommentLength
                                 + item.PostfixCommentLength
                                 + ((item.NameLength > 0) ? 1 : 0) // Possible colon
                                 + item.Children.Sum(ch => ch.MinimumTotalLength)
                                 + bracketLength
                                 + Math.Max(0, item.Children.Count - 1); // 

        if (totalMinimumLength > int.MaxValue)
            throw new FracturedJsonException("Maximum document length exceeded");

        item.MinimumTotalLength = (int)totalMinimumLength;
    }

    private int StringLengthWithNullCheck(string? value)
    {
        return (value != null) ? StringLengthFunc(value) : 0;
    }

    private static bool IsMultilineComment(JsonItem item)
    {
        return item.Type == JsonItemType.BlockComment && item.Value != null && item.Value.Contains('\n');
    }

    private static JsonToken GetNextTokenOrThrown(IEnumerator<JsonToken> enumerator, InputPosition startPosition)
    {
        if (!enumerator.MoveNext())
            throw FracturedJsonException.Create("Unexpected end of input while processing array or object starting",
                startPosition);
        return enumerator.Current;
    }

    private static void AttachObjectValuePieces(List<JsonItem> objItemList, JsonToken name, JsonItem element,
        long valueEndingLine, List<JsonItem> beforeComments, List<JsonToken> midComments, List<JsonItem> afterComments)
    {
        element.Name = name.Text;

        // Deal with any comments between the property name and its element.  If there's more than one, squish them 
        // together.
        var combinedMiddleCommentText = string.Empty;
        var lastMiddleWasLineComment = false;
        foreach (var comment in midComments)
        {
            // Not even going to try to preserve this case.
            if (comment.Type == TokenType.BlankLine)
                continue;

            if (combinedMiddleCommentText.Length > 0)
                combinedMiddleCommentText += (lastMiddleWasLineComment) ? '\n' : ' ';
            combinedMiddleCommentText += comment.Text;
            lastMiddleWasLineComment = (comment.Type == TokenType.LineComment);
        }

        element.MiddleComment = (combinedMiddleCommentText.Length > 0) ? combinedMiddleCommentText : null;

        // Figure out if the last of the comments before the prop name should be attached to this element.
        // Any others should be added as unattached comment items.
        if (beforeComments.Count > 0)
        {
            var lastOfBefore = beforeComments[^1];
            if (lastOfBefore.Type == JsonItemType.BlockComment && lastOfBefore.InputLine == element.InputLine)
            {
                element.PrefixComment = lastOfBefore.Value;
                objItemList.AddRange(beforeComments.Take(beforeComments.Count - 1));
            }
            else
            {
                objItemList.AddRange(beforeComments);
            }
        }

        objItemList.Add(element);

        // Figure out if the first of the comments after the element should be attached to the element, and add 
        // the others as unattached comment items.
        if (afterComments.Count > 0)
        {
            var firstOfAfter = afterComments[0];
            if (!IsMultilineComment(firstOfAfter) && firstOfAfter.InputLine == valueEndingLine)
            {
                element.PostfixComment = firstOfAfter.Value;
                objItemList.AddRange(afterComments.Skip(1));
            }
            else
            {
                objItemList.AddRange(afterComments);
            }
        }

        beforeComments.Clear();
        midComments.Clear();
        afterComments.Clear();
    }

    private static int StringLengthByCharacterCount(string value)
    {
        return value.Length;
    }
    
    private enum CommaStatus
    {
        EmptyCollection,
        ElementSeen,
        CommaSeen,
    }

    private enum ObjectPhase
    {
        BeforePropName,
        AfterPropName,
        AfterColon,
        AfterPropValue,
        AfterComma,
    }
}
