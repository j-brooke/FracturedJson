using System;
using System.Collections.Generic;
using FracturedJson.Tokenizer;

namespace FracturedJson.V3;

/// <summary>
/// A distinct thing that can be where ever JSON values are expected in a JSON-with-comments doc.  This could be an 
/// actual data value, such as a string, number, array, etc. (generally referred to here as "elements"), or it could be
/// a blank line or standalone comment.  In some cases, comments won't be stand-alone JsonItems, but will instead
/// be attached to elements to which they seem to belong.
/// </summary>
public class JsonItem
{
    public JsonItemType Type { get; set; }
    
    /// <summary>
    /// Line number from the input - if available - where this element began.
    /// </summary>
    public InputPosition InputPosition { get; set; }

    /// <summary>
    /// Nesting level of this item's contents if any.  A simple item, or an empty array or object, has a complexity of
    /// zero.  Non-empty arrays/objects have a complexity 1 greater than that of their child with the greatest
    /// complexity.
    /// </summary>
    public int Complexity { get; set; }
    
    /// <summary>
    /// Property name, if this is an element (real JSON value) that is contained in an object.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// The text value of this item, non-recursively.  Null for objects and arrays.
    /// </summary>
    public string? Value { get; set; }
    
    /// <summary>
    /// Comment that belongs in front of this element on the same line, if any.
    /// </summary>
    public string? PrefixComment { get; set; }
    
    /// <summary>
    /// Comment (or, possibly many of them) that belongs in between the property name and value, if any.
    /// </summary>
    public string? MiddleComment { get; set; }
    
    /// <summary>
    /// Comment that belongs in front of this element on the same line, if any.
    /// </summary>
    public string? PostfixComment { get; set; }
    
    /// <summary>
    /// True if the postfix comment is to-end-of-line rather than block style.
    /// </summary>
    public bool IsPostCommentLineStyle { get; set; }
    
    public int NameLength { get; set; }
    public int ValueLength { get; set; }
    public int PrefixCommentLength { get; set; }
    public int MiddleCommentLength { get; set; }
    public int PostfixCommentLength { get; set; }
    
    /// <summary>
    /// The smallest possible size this item - including all comments and children if appropriate - can be written.
    /// </summary>
    public int MinimumTotalLength { get; set; }
    
    /// <summary>
    /// True if this item can't be written on a single line.
    /// </summary>
    public bool RequiresMultipleLines { get; set; }
    
    /// <summary>
    /// List of this item's contents, if it's an array or object.
    /// </summary>
    public IList<JsonItem> Children { get; set; } = Array.Empty<JsonItem>();
}
