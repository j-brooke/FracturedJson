using System;
using System.Collections.Generic;
using System.Linq;
using FracturedJson.Tokenizing;

namespace FracturedJson.Parsing;

/// <summary>
/// Class that takes JSON input (possibly with comments) and converts it to a forrest of <see cref="JsonItem"/> objects
/// representing the data and its structure.  Great pains are taken to try to keep comments with the elements to
/// which they seem to refer.
/// </summary>
public class Parser
{
    /// <summary>
    /// Settings controlling output and defining permissible input.
    /// </summary>
    public FracturedJsonOptions Options { get; set; } = new();

    /// <summary>
    /// Returns a sequence of <see cref="JsonItem"/>s representing the top-level items in the input.  In a typical
    /// JSON you're only allowed to have one top-level value, but since there might be comments or blank lines,
    /// we have to be able to return multiple things before or after the actual data.
    /// </summary>
    /// <param name="charEnumeration">The JSON (with comments, maybe) text</param>
    /// <param name="stopAfterFirstElem">If true, the enumeration ends when a single top-level element (real JSON value)
    /// is read.  </param>
    /// <returns>JsonItems representing the top-level data, comments, and blank lines from the input.</returns>
    public IEnumerable<JsonItem> ParseTopLevel(IEnumerable<char> charEnumeration, bool stopAfterFirstElem)
    {
        var tokenStream = TokenScanner.Scan(charEnumeration);
        return ParseTopLevel(tokenStream, stopAfterFirstElem);
    }

    private IEnumerable<JsonItem> ParseTopLevel(IEnumerable<JsonToken> tokenEnumeration, bool stopAfterFirstElem)
    {
        using var enumerator = tokenEnumeration.GetEnumerator();

        var topLevelElemSeen = false;
        while (true)
        {
            if (!enumerator.MoveNext())
                yield break;

            var item = ParseItem(enumerator);

            var isComment = item.Type is JsonItemType.BlockComment or JsonItemType.LineComment;
            var isBlank = item.Type is JsonItemType.BlankLine;

            if (isBlank)
            {
                if (Options.PreserveBlankLines)
                    yield return item;
            }
            else if (isComment)
            {
                if (Options.CommentPolicy == CommentPolicy.TreatAsError)
                    throw FracturedJsonException.Create("Comments not allowed with current options",
                        item.InputPosition);
                if (Options.CommentPolicy == CommentPolicy.Preserve)
                    yield return item;
            }
            else
            {
                if (topLevelElemSeen && stopAfterFirstElem)
                    throw FracturedJsonException.Create("Unexpected start of second top level element",
                        item.InputPosition);
                topLevelElemSeen = true;
                yield return item;
            }
        }
    }

    /// <summary>
    /// Parse a simple token (not an array or object) into a <see cref="JsonItem"/>. 
    /// </summary>
    private JsonItem ParseSimple(JsonToken token)
    {
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
            InputPosition = token.InputPosition,
            Complexity = 0,
        };

        return item;
    }

    /// <summary>
    /// Parse the stream of tokens into a JSON array (recursively).  The enumerator should be pointing to the open
    /// square bracket token at the start of the call.  It will be pointing to the closing bracket when the call
    /// returns.
    /// </summary>
    private JsonItem ParseArray(IEnumerator<JsonToken> enumerator)
    {
        if (enumerator.Current.Type != TokenType.BeginArray)
            throw FracturedJsonException.Create("Parser logic error", enumerator.Current.InputPosition);

        var startingInputPosition = enumerator.Current.InputPosition;

        // Holder for an element that was already added to the child list that is eligible for a postfix comment.
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
                                           (unplacedComment.InputPosition.Row != token.InputPosition.Row ||
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
                    elemNeedingPostComment.IsPostCommentLineStyle = unplacedComment.Type == JsonItemType.LineComment;
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
                    childList.Add(ParseSimple(token));
                    break;

                case TokenType.BlockComment:
                    if (Options.CommentPolicy == CommentPolicy.Remove)
                        break;
                    if (Options.CommentPolicy == CommentPolicy.TreatAsError)
                        throw FracturedJsonException.Create("Comments not allowed with current options",
                            token.InputPosition);

                    if (unplacedComment != null)
                    {
                        // There was a block comment before this one.  Add it as a standalone comment to make room.
                        childList.Add(unplacedComment);
                        unplacedComment = null;
                    }

                    // If this is a multiline comment, add it as standalone.
                    var commentItem = ParseSimple(token);
                    if (IsMultilineComment(commentItem))
                    {
                        childList.Add(commentItem);
                        break;
                    }

                    // If this comment came after an element and before a comma, attach it to that element.
                    if (elemNeedingPostComment != null && commaStatus == CommaStatus.ElementSeen)
                    {
                        elemNeedingPostComment.PostfixComment = commentItem.Value;
                        elemNeedingPostComment.IsPostCommentLineStyle = false;
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
                        // A previous comment followed by a line-ending comment?  Add them both as standalone comments
                        childList.Add(unplacedComment);
                        childList.Add(ParseSimple(token));
                        unplacedComment = null;
                        break;
                    }

                    if (elemNeedingPostComment != null)
                    {
                        // Since this is a line comment, we know there isn't anything else on the line after this.
                        // So if there was an element before this that can take a comment, attach it.
                        elemNeedingPostComment.PostfixComment = token.Text;
                        elemNeedingPostComment.IsPostCommentLineStyle = true;
                        elemNeedingPostComment = null;
                        break;
                    }

                    childList.Add(ParseSimple(token));
                    break;

                case TokenType.False:
                case TokenType.True:
                case TokenType.Null:
                case TokenType.String:
                case TokenType.Number:
                case TokenType.BeginArray:
                case TokenType.BeginObject:
                    if (commaStatus == CommaStatus.ElementSeen)
                        throw FracturedJsonException.Create("Comma missing while processing array",
                            token.InputPosition);
                    
                    var element = ParseItem(enumerator);
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

        var arrayItem = new JsonItem()
        {
            Type = JsonItemType.Array,
            InputPosition = startingInputPosition,
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
    private JsonItem ParseObject(IEnumerator<JsonToken> enumerator)
    {
        if (enumerator.Current.Type != TokenType.BeginObject)
            throw FracturedJsonException.Create("Parser logic error", enumerator.Current.InputPosition);

        var startingInputPosition = enumerator.Current.InputPosition;

        var childList = new List<JsonItem>();

        // Variables to collect the pieces as we go.  We'll put them all together and add them to the child list 
        // when conditions are appropriate.
        JsonToken? propertyName = null;
        JsonItem? propertyValue = null;
        var linePropValueEnds = -1;
        var beforePropComments = new List<JsonItem>();
        var midPropComments = new List<JsonToken>();
        JsonItem? afterPropComment = null;
        var afterPropCommentWasAfterComma = false;
        
        var phase = ObjectPhase.BeforePropName;
        var thisObjComplexity = 0;
        var endOfObject = false;
        while (!endOfObject)
        {
            var token = GetNextTokenOrThrown(enumerator, startingInputPosition);

            // We may have collected a bunch of stuff that should be combined into a single JsonItem.  If we have a
            // property name and value, then we're just waiting for potential postfix comments.  But it might be time
            // to bundle it all up and add it to childList before going on.
            var isNewLine = (linePropValueEnds != token.InputPosition.Row);
            var isEndOfObject = (token.Type == TokenType.EndObject);
            var startingNextPropName = (token.Type == TokenType.String && phase == ObjectPhase.AfterComma);
            var isExcessPostComment = (afterPropComment != null &&
                                       (token.Type == TokenType.BlockComment || token.Type == TokenType.LineComment));
            var needToFlush = propertyName != null && propertyValue != null &&
                              (isNewLine || isEndOfObject || startingNextPropName || isExcessPostComment);
            if (needToFlush)
            {
                JsonItem? commentToHoldForNextElement = null;
                if (startingNextPropName && afterPropCommentWasAfterComma && !isNewLine)
                {
                    // We've got an afterPropComment that showed up after the comma, and we're about to process
                    // another element on this same line.  The comment should go with the next one, to honor the
                    // comma placement.
                    commentToHoldForNextElement = afterPropComment;
                    afterPropComment = null;
                }

                AttachObjectValuePieces(childList, propertyName!.Value, propertyValue!, linePropValueEnds,
                    beforePropComments, midPropComments, afterPropComment);
                thisObjComplexity = Math.Max(thisObjComplexity, propertyValue!.Complexity + 1);
                propertyName = null;
                propertyValue = null;
                beforePropComments.Clear();
                midPropComments.Clear();
                afterPropComment = null;

                if (commentToHoldForNextElement != null)
                    beforePropComments.Add(commentToHoldForNextElement);
            }

            switch (token.Type)
            {
                case TokenType.BlankLine:
                    if (!Options.PreserveBlankLines)
                        break;
                    if (phase == ObjectPhase.AfterPropName || phase == ObjectPhase.AfterColon)
                        break;

                    // If we were hanging on to comments to maybe be prefix comments, add them as standalone before
                    // adding a blank line item.
                    childList.AddRange(beforePropComments);
                    beforePropComments.Clear();                        
                    childList.Add(ParseSimple(token));
                    break;
                case TokenType.BlockComment:
                case TokenType.LineComment:
                    if (Options.CommentPolicy==CommentPolicy.Remove)
                        break;
                    if (Options.CommentPolicy == CommentPolicy.TreatAsError)
                        throw FracturedJsonException.Create("Comments not allowed with current options",
                            token.InputPosition);
                    if (phase == ObjectPhase.BeforePropName || propertyName==null)
                    {
                        beforePropComments.Add(ParseSimple(token));
                    }
                    else if (phase == ObjectPhase.AfterPropName || phase == ObjectPhase.AfterColon)
                    {
                        midPropComments.Add(token);
                    }
                    else
                    {
                        afterPropComment = ParseSimple(token);
                        afterPropCommentWasAfterComma = (phase == ObjectPhase.AfterComma);
                    }
                    break;
                case TokenType.EndObject:
                    if (phase == ObjectPhase.AfterPropName || phase == ObjectPhase.AfterColon)
                        throw FracturedJsonException.Create("Unexpected end of object",
                            token.InputPosition);

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
                        propertyValue = ParseItem(enumerator);
                        linePropValueEnds = enumerator.Current.InputPosition.Row;
                        phase = ObjectPhase.AfterPropValue;
                    }
                    else
                    {
                        throw FracturedJsonException.Create("Unexpected string found while processing object",
                            token.InputPosition);
                    }
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
                    propertyValue = ParseItem(enumerator);
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

        var objItem = new JsonItem()
        {
            Type = JsonItemType.Object,
            InputPosition = startingInputPosition,
            Complexity = thisObjComplexity,
            Children = childList,
        };
        return objItem;
    }

    /// <summary>
    /// Parse the next thing, no matter what it is.
    /// </summary>
    private JsonItem ParseItem(IEnumerator<JsonToken> enumerator)
    {
        return enumerator.Current.Type switch
        {
            TokenType.BeginArray => ParseArray(enumerator),
            TokenType.BeginObject => ParseObject(enumerator),
            _ => ParseSimple(enumerator.Current),
        };
    }

    private static bool IsMultilineComment(JsonItem item)
    {
        return item.Type == JsonItemType.BlockComment && item.Value.Contains('\n');
    }

    private static JsonToken GetNextTokenOrThrown(IEnumerator<JsonToken> enumerator, InputPosition startPosition)
    {
        if (!enumerator.MoveNext())
            throw FracturedJsonException.Create("Unexpected end of input while processing array or object starting",
                startPosition);
        return enumerator.Current;
    }

    /// <summary>
    /// Given a loose collection of comments, a prop name, and a prop value, bundle them all up into a single JsonItem
    /// if possible and add it to the list.  (It's possible that some comments will need to be added as standalone items
    /// too.)
    /// </summary>
    private static void AttachObjectValuePieces(List<JsonItem> objItemList, JsonToken name, JsonItem element,
        int valueEndingLine, List<JsonItem> beforeComments, List<JsonToken> midComments, JsonItem? afterComment)
    {
        element.Name = name.Text;

        // Deal with any comments between the property name and its element.  If there's more than one, squish them 
        // together.  If it's a line comment, make sure it ends in a \n (which isn't how it's handled elsewhere, alas.)
        if (midComments.Count > 0)
        {
            var combined = string.Empty;
            for (var i = 0; i < midComments.Count; ++i)
            {
                combined += midComments[i].Text;
                if (i < midComments.Count - 1 || midComments[i].Type == TokenType.LineComment)
                    combined += '\n';
            }

            element.MiddleComment = combined;
        }
        
        
        // Figure out if the last of the comments before the prop name should be attached to this element.
        // Any others should be added as unattached comment items.
        if (beforeComments.Count > 0)
        {
            var lastOfBefore = beforeComments[beforeComments.Count-1];
            if (lastOfBefore.Type == JsonItemType.BlockComment && 
                lastOfBefore.InputPosition.Row == element.InputPosition.Row)
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
        if (afterComment != null)
        {
            if (!IsMultilineComment(afterComment) && afterComment.InputPosition.Row == valueEndingLine)
            {
                element.PostfixComment = afterComment.Value;
                element.IsPostCommentLineStyle = (afterComment.Type == JsonItemType.LineComment);
            }
            else
            {
                objItemList.Add(afterComment);
            }
        }
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
