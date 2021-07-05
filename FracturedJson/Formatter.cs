/*
 * FracturedJson
 * FracturedJson is a library for formatting JSON documents in a human-readable but fairly compact way.
 *
 * Copyright (c) 2021 Jesse Brooke
 * Project site: https://github.com/j-brooke/FracturedJson
 * License: https://github.com/j-brooke/FracturedJson/blob/main/LICENSE
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FracturedJson
{
    /// <summary>
    /// Specifies what sort of line endings to use.
    /// </summary>
    public enum EolStyle
    {
        /// <summary>
        /// The native environment's line endings will be used.
        /// </summary>
        Default,

        /// <summary>
        /// Carriage Return, followed by a line feed.  Windows-style.
        /// </summary>
        Crlf,

        /// <summary>
        /// Just a line feed.  Unix-style.
        /// </summary>
        Lf,
    }

    public class Formatter
    {
        /// <summary>
        /// Dictates what sort of line endings to use.
        /// </summary>
        public EolStyle JsonEolStyle { get; set; } = EolStyle.Default;

        /// <summary>
        /// Maximum length of a complex element on a single line.  This includes only the data for the inlined element,
        /// not indentation or leading property names.
        /// </summary>
        public int MaxInlineLength { get; set; } = 80;

        /// <summary>
        /// Maximum nesting level that can be displayed on a single line.  A primitive type or an empty
        /// array or object has a complexity of 0.  An object or array has a complexity of 1 greater than
        /// its most complex child.
        /// </summary>
        public int MaxInlineComplexity { get; set; } = 2;

        /// <summary>
        /// Maximum nesting level that can be arranged spanning multiple lines, with multiple items per line.
        /// </summary>
        public int MaxCompactArrayComplexity { get; set; } = 1;

        public bool NestedBracketPadding { get; set; } = true;

        /// <summary>
        /// If true, includes a space after property colons.
        /// </summary>
        public bool ColonPadding { get; set; } = true;

        /// <summary>
        /// If true, includes a space after commas separating array items and object properties.
        /// </summary>
        public bool CommaPadding { get; set; } = true;

        public int IndentSpaces { get; set; } = 4;
        public bool UseTabToIndent { get; set; } = false;

        /// <summary>
        /// Depth at which lists/objects are always fully expanded, regardless of other settings.
        /// -1 = none; 0 = root node only; 1 = root node and its children.
        /// </summary>
        public int AlwaysExpandDepth { get; set; } = -1;

        public double TableObjectMinimumSimliarity { get; set; } = 75.0;
        public double TableArrayMinimumSimilarity { get; set; } = 75.0;

        public bool JustifyExpandedPropertyNames { get; set; } = false;

        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// Returns the JSON documented formatted as a string, with simpler collections written in single
        /// lines where possible.
        /// </summary>
        public string Serialize(JsonDocument document)
        {
            InitInternals();
            return FormatElement(0, document.RootElement).Value;
        }

        /// <summary>
        /// Returns a reformatted version of the given JSON string.
        /// </summary>
        public string Serialize(string jsonData)
        {
            var parserOpts = new JsonDocumentOptions() {CommentHandling = JsonCommentHandling.Skip};
            var jsonDoc = JsonDocument.Parse(jsonData, parserOpts);
            return Serialize(jsonDoc);
        }

        /// <summary>
        /// Returns the JSON documented formatted as a string, with simpler collections written in single
        /// lines where possible.
        /// </summary>
        public string Serialize<T>(T obj)
        {
            var doc = JsonSerializer.Serialize(obj, JsonSerializerOptions);
            return Serialize(doc);
        }

        // We reuse this same StringBuilder throughout all levels of recursion.  It's important, therefore,
        // not to recurse while we're actually using it.
        private readonly StringBuilder _buff = new StringBuilder();

        private string _eolStr = string.Empty;
        private string _indentStr = string.Empty;
        private string _paddedCommaStr = string.Empty;
        private string _paddedColonStr = string.Empty;

        /// <summary>
        /// Set up some intermediate fields for efficiency.
        /// </summary>
        private void InitInternals()
        {
            _eolStr = JsonEolStyle switch
            {
                EolStyle.Crlf => "\r\n",
                EolStyle.Lf => "\n",
                _ => Environment.NewLine,
            };

            _indentStr = (UseTabToIndent) ? "\t" : new string(' ', IndentSpaces);
            _paddedCommaStr = (CommaPadding) ? ", " : ",";
            _paddedColonStr = (ColonPadding) ? ": " : ":";
        }

        /// <summary>
        /// Base of recursion.  Nearly everything comes through here.
        /// </summary>
        private FormattedNode FormatElement(int depth, JsonElement element)
        {
            var formattedItem = element.ValueKind switch
            {
                JsonValueKind.Array => FormatArray(depth, element),
                JsonValueKind.Object => FormatObject(depth, element),
                _ => FormatSimple(depth, element)
            };

            // Get rid of nested data that we don't need any more.
            Cleanup(formattedItem);
            return formattedItem;
        }

        private FormattedNode FormatSimple(int depth, JsonElement element)
        {
            // Return the existing text of the item.  Since it's not an array or object, there won't be any ambiguous
            // whitespace in it.
            return new FormattedNode()
            {
                Value = element.GetRawText(),
                Depth = depth,
                Format = Format.Inline,
                Kind = element.ValueKind
            };
        }

        private FormattedNode FormatArray(int depth, JsonElement element)
        {
            var items = element.EnumerateArray()
                .Select(child => FormatElement(depth + 1, child))
                .ToArray();

            if (!items.Any())
                return EmptyArray(depth);

            var thisItem = new FormattedNode()
            {
                Kind = JsonValueKind.Array,
                Complexity = items.Select(fn => fn.Complexity).Max() + 1,
                Depth = depth,
                Children = items,
            };

            if (thisItem.Depth > AlwaysExpandDepth)
            {
                if (FormatArrayInline(thisItem))
                    return thisItem;

                if (FormatArrayMultilineCompact(thisItem))
                    return thisItem;
            }

            if (FormatTableArrayObject(thisItem))
                return thisItem;

            if (FormatTableArrayArray(thisItem))
                return thisItem;

            FormatArrayExpanded(thisItem);
            return thisItem;
        }

        private FormattedNode FormatObject(int depth, JsonElement element)
        {
            var items = element.EnumerateObject()
                .Select(child => FormatElement(depth+1, child.Value).WithName(child.Name))
                .ToArray();

            if (!items.Any())
                return EmptyObject(depth);

            var thisItem = new FormattedNode()
            {
                Kind = JsonValueKind.Object,
                Complexity = items.Select(fn => fn.Complexity).Max() + 1,
                Depth = depth,
                Children = items,
            };

            if (thisItem.Depth > AlwaysExpandDepth)
            {
                if (FormatObjectInline(thisItem))
                    return thisItem;
            }

            if (FormatTableObjectObject(thisItem))
                return thisItem;

            if (FormatTableObjectArray(thisItem))
                return thisItem;

            FormatObjectExpanded(thisItem, false);
            return thisItem;
        }

        private FormattedNode EmptyArray(int depth)
        {
            return new FormattedNode()
            {
                Value = "[]",
                Complexity = 0,
                Depth = depth,
                Kind = JsonValueKind.Array,
                Format = Format.Inline,
            };
        }

        /// <summary>
        /// Try to format this array in a single line, if possible.
        /// </summary>
        private bool FormatArrayInline(FormattedNode thisItem)
        {
            if (thisItem.Complexity > MaxInlineComplexity)
                return false;

            var useNestedBracketPadding = (NestedBracketPadding && thisItem.Complexity >= 2);
            var lineLength = 2 + (useNestedBracketPadding ? 2 : 0)                              // outer brackets
                               + (thisItem.Children.Count - 1) * _paddedCommaStr.Length            // commas
                               + thisItem.Children.Sum(fn => fn.Value.Length);  // values
            if (lineLength > MaxInlineLength)
                return false;

            _buff.Clear();
            _buff.Append('[');

            if (useNestedBracketPadding)
                _buff.Append(' ');

            var firstElem = true;
            foreach (var child in thisItem.Children)
            {
                if (!firstElem)
                    _buff.Append(_paddedCommaStr);
                _buff.Append(child.Value);
                firstElem = false;
            }

            if (useNestedBracketPadding)
                _buff.Append(' ');

            _buff.Append(']');
            if (lineLength != _buff.Length)
                throw new Exception("Logic error with length prediction");

            thisItem.Value = _buff.ToString();
            thisItem.Format = Format.Inline;
            return true;
        }

        /// <summary>
        /// Try to format this array, spanning multiple lines, but with several items per line, if possible.
        /// </summary>
        private bool FormatArrayMultilineCompact(FormattedNode thisItem)
        {
            if (thisItem.Complexity > MaxCompactArrayComplexity)
                return false;

            _buff.Clear();
            _buff.Append('[').Append(_eolStr);
            Indent(_buff, thisItem.Depth + 1);

            var lineLengthSoFar = 0;
            var childIndex = 0;
            while (childIndex < thisItem.Children.Count)
            {
                var notLastItem = childIndex < thisItem.Children.Count - 1;

                var itemLength = thisItem.Children[childIndex].Value.Length;
                var segmentLength = itemLength + ((notLastItem)? _paddedCommaStr.Length : 0);
                if (lineLengthSoFar + segmentLength > MaxInlineLength && lineLengthSoFar > 0)
                {
                    _buff.Append(_eolStr);
                    Indent(_buff, thisItem.Depth + 1);
                    lineLengthSoFar = 0;
                }

                _buff.Append(thisItem.Children[childIndex].Value);
                if (notLastItem)
                    _buff.Append(_paddedCommaStr);

                childIndex += 1;
                lineLengthSoFar += segmentLength;
            }

            _buff.Append(_eolStr);
            Indent(_buff, thisItem.Depth);
            _buff.Append(']');

            thisItem.Value = _buff.ToString();
            thisItem.Format = Format.MultilineCompact;
            return true;
        }

        /// <summary>
        /// Format this array with one child object per line, and those objects padded to line up nicely.
        /// </summary>
        private bool FormatTableArrayObject(FormattedNode thisItem)
        {
            // Gather stats about our children's property order and width, if they're eligible objects.
            var propStats = GetPropertyStats(thisItem);
            if (propStats == null)
                return false;

            // Reformat our immediate children using the width info we've computed.  Their children aren't
            // recomputed, so this part isn't recursive.
            foreach (var child in thisItem.Children)
                FormatObjectTableRow(child, propStats);

            return FormatArrayExpanded(thisItem);
        }

        /// <summary>
        /// Format this array with one child array per line, and those arrays padded to line up nicely.
        /// </summary>
        private bool FormatTableArrayArray(FormattedNode thisItem)
        {
            // Gather stats about our children's item widths, if they're eligible arrays.
            var columnWidths = GetArrayStats(thisItem);
            if (columnWidths == null)
                return false;

            // Reformat our immediate children using the width info we've computed.  Their children aren't
            // recomputed, so this part isn't recursive.
            foreach (var child in thisItem.Children)
                FormatArrayTableRow(child, columnWidths);

            return FormatArrayExpanded(thisItem);
        }

        /// <summary>
        /// Format this array in a single line, with padding to line up with siblings.
        /// </summary>
        private void FormatArrayTableRow(FormattedNode thisItem, int[] columnSizes)
        {
            _buff.Clear();
            _buff.Append("[ ");

            // Write the elements that actually exist in this array.
            for (var index = 0; index < thisItem.Children.Count; ++index)
            {
                if (index != 0)
                    _buff.Append(_paddedCommaStr);
                var padSize = columnSizes[index] - thisItem.Children[index].Value.Length;
                _buff.Append(thisItem.Children[index].Value).Append(' ', padSize);
            }

            // Write padding for the others, to line up with siblings.
            for (var index = thisItem.Children.Count; index < columnSizes.Length; ++index)
            {
                var padSize = columnSizes[index]
                              + ((index == 0) ? 0 : _paddedCommaStr.Length);
                _buff.Append(' ', padSize);
            }

            _buff.Append(" ]");

            thisItem.Value = _buff.ToString();
            thisItem.Format = Format.InlineTabular;
        }

        /// <summary>
        /// Write this array with each element starting on its own line.  (They might be multiple lines themselves.)
        /// </summary>
        private bool FormatArrayExpanded(FormattedNode thisItem)
        {
            _buff.Clear();
            _buff.Append('[').Append(_eolStr);
            var firstElem = true;
            foreach (var child in thisItem.Children)
            {
                if (!firstElem)
                    _buff.Append(',').Append(_eolStr);
                Indent(_buff, child.Depth).Append(child.Value);
                firstElem = false;
            }

            _buff.Append(_eolStr);
            Indent(_buff, thisItem.Depth).Append(']');

            thisItem.Value = _buff.ToString();
            thisItem.Format = Format.Expanded;
            return true;
        }

        private FormattedNode EmptyObject(int depth)
        {
            return new FormattedNode()
            {
                Value = "{}",
                Complexity = 0,
                Depth = depth,
                Kind = JsonValueKind.Object,
                Format = Format.Inline,
            };
        }

        /// <summary>
        /// Format this object as a single line, if possible.
        /// </summary>
        private bool FormatObjectInline(FormattedNode thisItem)
        {
            if (thisItem.Complexity > MaxInlineComplexity)
                return false;

            var useNestedBracketPadding = (NestedBracketPadding && thisItem.Complexity >= 2);

            var lineLength = 2 + (useNestedBracketPadding ? 2 : 0)                              // outer brackets
                               + thisItem.Children.Count * _paddedColonStr.Length                  // colons
                               + (thisItem.Children.Count - 1) * _paddedCommaStr.Length            // commas
                               + thisItem.Children.Count * 2                                       // prop quotes
                               + thisItem.Children.Sum(fn => fn.Name.Length)    // propnames
                               + thisItem.Children.Sum(fn => fn.Value.Length);  // values
            if (lineLength > MaxInlineLength)
                return false;

            _buff.Clear();
            _buff.Append('{');

            if (useNestedBracketPadding)
                _buff.Append(' ');

            var firstElem = true;
            foreach (var prop in thisItem.Children)
            {
                if (!firstElem)
                    _buff.Append(_paddedCommaStr);
                _buff.Append('"').Append(prop.Name).Append('"').Append(_paddedColonStr).Append(prop.Value);
                firstElem = false;
            }

            if (useNestedBracketPadding)
                _buff.Append(' ');

            _buff.Append('}');

            if (lineLength != _buff.Length)
                throw new Exception("Logic error with length prediction");

            thisItem.Value = _buff.ToString();
            thisItem.Format = Format.Inline;
            return true;
        }

        /// <summary>
        /// Format this object with one child object per line, and those objects padded to line up nicely.
        /// </summary>
        private bool FormatTableObjectObject(FormattedNode thisItem)
        {
            // Gather stats about our children's property order and width, if they're eligible objects.
            var propStats = GetPropertyStats(thisItem);
            if (propStats == null)
                return false;

            // Reformat our immediate children using the width info we've computed.  Their children aren't
            // recomputed, so this part isn't recursive.
            foreach (var child in thisItem.Children)
                FormatObjectTableRow(child, propStats);

            return FormatObjectExpanded(thisItem, true);
        }

        /// <summary>
        /// Format this object with one child array per line, and those arrays padded to line up nicely.
        /// </summary>
        private bool FormatTableObjectArray(FormattedNode thisItem)
        {
            // Gather stats about our children's widths, if they're eligible arrays.
            var columnWidths = GetArrayStats(thisItem);
            if (columnWidths == null)
                return false;

            // Reformat our immediate children using the width info we've computed.  Their children aren't
            // recomputed, so this part isn't recursive.
            foreach (var child in thisItem.Children)
                FormatArrayTableRow(child, columnWidths);

            return FormatObjectExpanded(thisItem, true);
        }

        /// <summary>
        /// Format this object in a single line, with padding to line up with siblings.
        /// </summary>
        private void FormatObjectTableRow(FormattedNode thisItem, PropertyStats[] propLengths)
        {
            // Bundle up each property name, value, quotes, colons, etc., or equivalent empty space.
            var highestNonBlankIndex = -1;
            var propSegmentStrings = new string?[propLengths.Length];
            for (var propIndex = 0; propIndex < propLengths.Length; ++propIndex)
            {
                _buff.Clear();
                var propStat = propLengths[propIndex];
                var propNode = thisItem.Children.FirstOrDefault(pn => pn.Name == propStat.Name);
                if (propNode == null)
                {
                    var skipLength = 2
                                     + propStat.Name.Length
                                     + _paddedColonStr.Length
                                     + propStat.MaxValueSize;
                    _buff.Append(' ', skipLength);
                }
                else
                {
                    var valuePadLength = propStat.MaxValueSize - propNode.Value.Length;
                    _buff.Append('"').Append(propStat.Name).Append('"')
                        .Append(_paddedColonStr).Append(propNode.Value).Append(' ', valuePadLength);
                    highestNonBlankIndex = propIndex;
                }

                propSegmentStrings[propIndex] = _buff.ToString();
            }

            _buff.Clear();
            _buff.Append("{ ");

            // Put them all together with commas in the right places.
            var firstElem = true;
            var needsComma = false;
            for (var segmentIndex = 0; segmentIndex<propSegmentStrings.Length; ++segmentIndex)
            {
                if (needsComma && segmentIndex<=highestNonBlankIndex)
                    _buff.Append(_paddedCommaStr);
                else if (!firstElem)
                    _buff.Append(' ', _paddedCommaStr.Length);
                _buff.Append(propSegmentStrings[segmentIndex]);
                needsComma = !string.IsNullOrWhiteSpace(propSegmentStrings[segmentIndex]);
                firstElem = false;
            }

            _buff.Append(" }");

            thisItem.Value = _buff.ToString();
            thisItem.Format = Format.InlineTabular;
        }

        /// <summary>
        /// Write this object with each element starting on its own line.  (They might be multiple lines
        /// themselves.)
        /// </summary>
        private bool FormatObjectExpanded(FormattedNode thisItem, bool forceExpandPropNames)
        {
            var maxPropNameLength = thisItem.Children.Max(fn => fn.Name.Length);
            _buff.Clear();

            _buff.Append('{').Append(_eolStr);

            var firstItem = true;
            foreach (var prop in thisItem.Children)
            {
                if (!firstItem)
                    _buff.Append(',').Append(_eolStr);
                Indent(_buff, prop.Depth).Append('"').Append(prop.Name).Append('"');

                if (JustifyExpandedPropertyNames || forceExpandPropNames)
                    _buff.Append(' ', maxPropNameLength - prop.Name.Length);

                _buff.Append(_paddedColonStr).Append(prop.Value);
                firstItem = false;
            }

            _buff.Append(_eolStr);
            Indent(_buff, thisItem.Depth).Append('}');

            thisItem.Value = _buff.ToString();
            thisItem.Format = Format.Expanded;
            return true;
        }

        private StringBuilder Indent(StringBuilder buff, int depth)
        {
            for (var i = 0; i < depth; ++i)
                buff.Append(_indentStr);
            return buff;
        }

        /// <summary>
        /// Deleted data we don't need any more, to possibly ease memory pressure.
        /// </summary>
        private void Cleanup(FormattedNode thisItem)
        {
            // We need to keep the children of inlined nodes, in case we decide to reformat them as a table.
            // Everything else can go.
            if (thisItem.Format != Format.Inline)
                thisItem.Children = Array.Empty<FormattedNode>();
            foreach (var child in thisItem.Children)
                child.Children = Array.Empty<FormattedNode>();
        }

        /// <summary>
        /// Check if this node's object children can be formatted as a table, and if so, return stats about
        /// their properties.  Returns null if they're not eligible.
        /// </summary>
        private PropertyStats[]? GetPropertyStats(FormattedNode thisItem)
        {
            // Record every property across all objects, count them, tabulate their order, and find the longest.
            var props = new Dictionary<string, PropertyStats>();
            foreach (var child in thisItem.Children)
            {
                if (child.Kind != JsonValueKind.Object || child.Format != Format.Inline)
                    return null;

                for (var index=0; index<child.Children.Count; ++index)
                {
                    var propNode = child.Children[index];
                    props.TryGetValue(propNode.Name, out var propStats);
                    if (propStats == null)
                    {
                        propStats = new PropertyStats() {Name = propNode.Name};
                        props.Add(propStats.Name, propStats);
                    }

                    propStats.OrderSum += index;
                    propStats.Count += 1;
                    propStats.MaxValueSize = Math.Max(propStats.MaxValueSize, propNode.Value.Length);
                }
            }

            // Decide the order of the properties by sorting by the average index.  It's a crude metric,
            // but it should handle the occasional missing property well enough.
            var orderedProps = props.Values
                .OrderBy(ps => (ps.OrderSum / (double) ps.Count))
                .ToArray();

            // Calculate a score based on how many of all possible properties are present.  If the score is too
            // low, these objects are too different to try to line up as a table.
            var score = 100.0 * orderedProps.Sum(ps => ps.Count)
                        / (orderedProps.Length * thisItem.Children.Count);
            if (score < TableObjectMinimumSimliarity)
                return null;

            // If the formatted lines would be too long, bail out.
            var lineLength = 4                                                        // outer brackets & spaces
                             + 2 * orderedProps.Length                                   // property quotes
                             + orderedProps.Sum(ps => ps.Name.Length)  // prop names
                             + _paddedColonStr.Length * orderedProps.Length              // colons
                             + orderedProps.Sum(ps => ps.MaxValueSize) // values
                             + _paddedCommaStr.Length * (orderedProps.Length - 1);       // commas
            if (lineLength > MaxInlineLength)
                return null;

            return orderedProps;
        }

        /// <summary>
        /// Check if this node's array children can be formatted as a table, and if so, the max length of each.
        /// Returns null if they're not eligible.
        /// </summary>
        private int[]? GetArrayStats(FormattedNode thisItem)
        {
            var valid = thisItem.Children.All(fn => fn.Kind == JsonValueKind.Array && fn.Format == Format.Inline);
            if (!valid)
                return null;

            var numberOfColumns = thisItem.Children.Max(fn => fn.Children.Count);
            var columnWidths = new int[numberOfColumns];
            foreach (var child in thisItem.Children)
            {
                for (var index = 0; index < child.Children.Count; ++index)
                    columnWidths[index] = Math.Max(columnWidths[index], child.Children[index].Value.Length);
            }

            // Calculate a score based on how rectangular the arrays are.  If they differ too much in length,
            // it probably doesn't make sense to format them together.
            var similarity = 100.0 * thisItem.Children.Sum(fn => fn.Children.Count)
                             / (thisItem.Children.Count * numberOfColumns);
            if (similarity < TableArrayMinimumSimilarity)
                return null;

            // If the formatted lines would be too long, bail out.
            var lineLength = 4                                    // outer brackets
                + columnWidths.Sum()                                 // values
                + columnWidths.Length - 1 * _paddedCommaStr.Length;  // commas
            if (lineLength > MaxInlineLength)
                return null;

            return columnWidths;
        }

        private enum Format
        {
            Inline,
            InlineTabular,
            MultilineCompact,
            Expanded,
        }

        private class FormattedNode
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = String.Empty;
            public int Complexity { get; set; }
            public int Depth { get; set; }
            public JsonValueKind Kind { get; set; } = JsonValueKind.Undefined;
            public Format Format { get; set; } = Format.Inline;
            public IList<FormattedNode> Children { get; set; } = Array.Empty<FormattedNode>();

            public FormattedNode WithName(string name)
            {
                Name = name;
                return this;
            }
        }

        /// <summary>
        /// Used in figuring out how to format objects as table rows.
        /// </summary>
        private class PropertyStats
        {
            public string Name { get; set; } = string.Empty;
            public int OrderSum { get; set; }
            public int Count { get; set; }
            public int MaxValueSize { get; set; }
        }
    }
}
