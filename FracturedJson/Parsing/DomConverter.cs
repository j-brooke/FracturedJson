using System.Linq;
using System.Text.Json;

namespace FracturedJson.Parsing;

public static class DomConverter
{
    public static JsonItem Convert(JsonElement dotnetElem, string? propName)
    {
        var itemType = dotnetElem.ValueKind switch
        {
            JsonValueKind.Array => JsonItemType.Array,
            JsonValueKind.Object => JsonItemType.Object,
            JsonValueKind.String => JsonItemType.String,
            JsonValueKind.Number => JsonItemType.Number,
            JsonValueKind.True => JsonItemType.True,
            JsonValueKind.False => JsonItemType.False,
            JsonValueKind.Null => JsonItemType.Null,
            _ => throw new FracturedJsonException("Unable to convert document"),
        };

        var item = new JsonItem()
        {
            Type = itemType,
            Name = (propName==null) ? string.Empty : "\"" + propName + "\"",
        };

        if (itemType is JsonItemType.Array)
            item.Children = dotnetElem.EnumerateArray().Select(elem => Convert(elem, null)).ToArray();
        else if (itemType is JsonItemType.Object)
            item.Children = dotnetElem.EnumerateObject().Select(kvp => Convert(kvp.Value, kvp.Name)).ToArray();
        else
            item.Value = dotnetElem.GetRawText() ?? throw new FracturedJsonException("Logic error converting doc");

        if (item.Children.Any())
            item.Complexity = item.Children.Max(ch => ch.Complexity) + 1;

        return item;
    }
}