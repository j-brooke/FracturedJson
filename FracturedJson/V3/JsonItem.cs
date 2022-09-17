using System;
using System.Collections.Generic;

namespace FracturedJson.V3;

public class JsonItem
{
    public JsonItemType Type { get; set; }
    public long InputLine { get; set; }
    public int Depth { get; set; }
    public int Complexity { get; set; }
    public string? Name { get; set; }
    public string? Value { get; set; }
    public string? PrefixComment { get; set; }
    public string? MiddleComment { get; set; }
    public string? PostfixComment { get; set; }
    public bool IsPostCommentBlockStyle { get; set; }
    public int NameLength { get; set; }
    public int ValueLength { get; set; }
    public int PreNameCommentLength { get; set; }
    public int PreValueCommentLength { get; set; }
    public int PostValueCommentLength { get; set; }
    public IList<JsonItem> Children { get; set; } = Array.Empty<JsonItem>();
}
