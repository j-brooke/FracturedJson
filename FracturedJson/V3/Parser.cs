using System;
using System.Collections.Generic;
using System.Linq;
using FracturedJson.Tokenizer;

namespace FracturedJson.V3;

public class Parser
{
    public FracturedJsonOptions Options { get; set; } = new();
    public Func<string,int> StringLengthFunc = (s) => s.Length;

    public IEnumerable<JsonItem> ParseTopLevel(IEnumerable<char> charEnumeration, int startingDepth,
        bool stopAfterFirstElem)
    {
        var tokenStream = TokenScanner.Scan(charEnumeration);
        return ParseTopLevel(tokenStream, startingDepth, stopAfterFirstElem);
    }

    public IEnumerable<JsonItem> ParseTopLevel(IEnumerable<JsonToken> tokenEnumeration, int startingDepth, 
        bool stopAfterFirstElem)
    {
        using var enumerator = tokenEnumeration.GetEnumerator();
        while (true)
        {
            if (!enumerator.MoveNext())
                yield break;

            var item = ParseItem(enumerator, startingDepth);
            yield return item;
            var isElement = item.Type != JsonItemType.BlankLine && item.Type != JsonItemType.BlockComment &&
                            item.Type != JsonItemType.LineComment;
            if (isElement && stopAfterFirstElem)
                yield break;
        }
    }

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
    
    private JsonItem ParseArray(IEnumerator<JsonToken> enumerator, int depth)
    {
        if (enumerator.Current.Type != TokenType.BeginArray)
            throw FracturedJsonException.Create("Parser logic error", enumerator.Current.InputPosition);

        var startingInputPosition = enumerator.Current.InputPosition;
        
        // An element that was already added to the child list that is eligible for a postfix comment.
        JsonItem? elemNeedingPostComment = null;

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
                    // I guess this is a block comment, after the final comma of a line
                    elemNeedingPostComment.PostfixComment = unplacedComment!.Value;
                    elemNeedingPostComment.IsPostCommentBlockStyle = unplacedComment.Type == JsonItemType.BlockComment;
                    
                }
                else
                {
                    // More than one block comment at the end of a single line, maybe?
                    childList.Add(unplacedComment!);
                }
                unplacedComment = null;
            }

            // If the token we're about to deal with isn't on the same line as the last element, the new token obviously 
            // won't be a postfix comment.
            if (elemNeedingPostComment != null && elemNeedingPostComment.InputLine != token.InputPosition.Row)
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

                    // If this is a multiline comment, add it as unattached.  Otherwise, hold it for later decision.
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
                    elemNeedingPostComment = element;
                    break;
                
                default:
                    throw FracturedJsonException.Create("Unexpected token in array", token.InputPosition);
            }
        }

        var arrayItem = new JsonItem()
        {
            Type = JsonItemType.Array,
            InputLine = startingInputPosition.Row,
            Depth = depth,
            Complexity = thisArrayComplexity,
            Children = childList,
        };

        // TODO: add up minimum lengths and eligibility?
        return arrayItem;
    }

    private JsonItem ParseObject(IEnumerator<JsonToken> enumerator, int depth)
    {
        if (enumerator.Current.Type != TokenType.BeginObject)
            throw FracturedJsonException.Create("Parser logic error", enumerator.Current.InputPosition);

        var startingInputPosition = enumerator.Current.InputPosition;

        var childList = new List<JsonItem>();
        
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
            }
            
            switch (token.Type)
            {
                case TokenType.BlankLine:
                case TokenType.BlockComment:
                case TokenType.LineComment:
                    if (phase==ObjectPhase.BeforePropName)
                        beforePropComments.Add(ParseSimple(enumerator, depth + 1));
                    else if (phase==ObjectPhase.AfterPropName || phase==ObjectPhase.AfterColon)
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
        
        if (!Options.AllowTrailingCommas && phase==ObjectPhase.AfterComma)
            throw FracturedJsonException.Create("Object may not end with comma with current options",
                enumerator.Current.InputPosition);

        // TODO: add up minimum lengths and eligibility?

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
                objItemList.AddRange(beforeComments.Take(beforeComments.Count-1));
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
