/*
 * FracturedJson
 * FracturedJson is a library for formatting JSON documents that produces human-readable but fairly compact output.
 *
 * Copyright (c) 2021 Jesse Brooke
 * Project site: https://github.com/j-brooke/FracturedJson
 * License: https://github.com/j-brooke/FracturedJson/blob/main/LICENSE
 */

using System;
using System.Collections.Generic;
using System.Globalization;
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


    /// <summary>
    /// Class that outputs JSON formatted in a compact, user-readable way.  Any given container is formatted in one
    /// of three ways:
    /// <list type="bullet">
    ///   <item>
    ///     <description>Arrays or objects will be written on a single line, if their contents aren't too complex
    ///     and the resulting line wouldn't be too long.</description>
    ///   </item>
    ///   <item>
    ///     <description>Arrays can be written on multiple lines, with multiple items per line, as long as those
    ///     items aren't too complex.</description>
    ///   </item>
    ///   <item>
    ///     <description>Otherwise, each object property or array item is written begining on its own line, indented
    ///     one step deeper than its parent.</description>
    ///   </item>
    /// </list>
    /// </summary>
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

        /// <summary>
        /// If an inlined array or object contains other arrays or objects, setting NestedBracketPadding to true
        /// will include spaces inside the outer brackets.
        /// </summary>
        /// <remarks>
        /// Example: <br/>
        /// true: [ [1, 2, 3], [4] ] <br/>
        /// false: [[1, 2, 3], [4]] <br/>
        /// </remarks>
        public bool NestedBracketPadding { get; set; } = true;

        /// <summary>
        /// If true, includes a space after property colons.
        /// </summary>
        public bool ColonPadding { get; set; } = true;

        /// <summary>
        /// If true, includes a space after commas separating array items and object properties.
        /// </summary>
        public bool CommaPadding { get; set; } = true;

        /// <summary>
        /// Depth at which lists/objects are always fully expanded, regardless of other settings.
        /// -1 = none; 0 = root node only; 1 = root node and its children.
        /// </summary>
        public int AlwaysExpandDepth { get; set; } = -1;

        /// <summary>
        /// Number of spaces to use per indent level (unless UseTabToIndent is true)
        /// </summary>
        public int IndentSpaces { get; set; } = 4;

        /// <summary>
        /// Uses a single tab per indent level, instead of spaces.
        /// </summary>
        public bool UseTabToIndent { get; set; } = false;

        /// <summary>
        /// Value from 0 to 100 indicating how similar collections of inline objects need to be to be formatted as
        /// a table.  A group of objects that don't have any property names in common has a similarity of zero.  A
        /// group of objects that all contain the exact same property names has a similarity of 100.  Setting this
        /// to a value &gt;100 disables table formatting with objects as rows.
        /// </summary>
        public double TableObjectMinimumSimliarity { get; set; } = 75.0;

        /// <summary>
        /// Value from 0 to 100 indicating how similar collections of inline arrays need to be to be formatted as
        /// a table.  Similarity for arrays refers to how similar they are in length; if they all have the same
        /// length their similarity is 100.  Setting this to a value &gt;100 disables table formatting with arrays as
        /// rows.
        /// </summary>
        public double TableArrayMinimumSimilarity { get; set; } = 75.0;

        /// <summary>
        /// If true, property names of expanded objects are padded to the same size.
        /// </summary>
        public bool AlignExpandedPropertyNames { get; set; } = false;

        /// <summary>
        /// If true, numbers won't be right-aligned with matching precision.
        /// </summary>
        public bool DontJustifyNumbers { get; set; } = false;

        /// <summary>
        /// String attached to the beginning of every line, before regular indentation.
        /// </summary>
        public string PrefixString { get; set; } = string.Empty;

        /// <summary>
        /// Options to pass on to the underlying system parser.
        /// </summary>
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
            return PrefixString + FormatElement(0, document.RootElement).Value;
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
        /// Returns the given object serialized to JSON and then formatted with simpler collections written in
        /// single lines where possible.
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

        /// <summary>
        /// Formats a JSON element other than an array or object.
        /// </summary>
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
            // Recursively format all of this array's elements.
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
            }

            // If this is an array of numbers, try to format them with uniform precision and padding.
            JustifyParallelNumbers(thisItem.Children);

            if (thisItem.Depth > AlwaysExpandDepth)
            {
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
            // Recursively format all of this object's property values.
            var items = element.EnumerateObject()
                .Select(child => FormatElement(depth + 1, child.Value).WithName(child.Name))
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
            var lineLength = 2 + (useNestedBracketPadding ? 2 : 0) +                       // outer brackets
                             (thisItem.Children.Count - 1) * _paddedCommaStr.Length +         // commas
                             thisItem.Children.Sum(fn => fn.Value.Length); // values
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
                var segmentLength = itemLength + ((notLastItem) ? _paddedCommaStr.Length : 0);
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
            if (TableObjectMinimumSimliarity > 100.5)
                return false;

            // Gather stats about our children's property order and width, if they're eligible objects.
            var columnStats = GetPropertyStats(thisItem);
            if (columnStats == null)
                return false;

            // Reformat our immediate children using the width info we've computed.  Their children aren't
            // recomputed, so this part isn't recursive.
            foreach (var child in thisItem.Children)
                FormatObjectTableRow(child, columnStats);

            return FormatArrayExpanded(thisItem);
        }

        /// <summary>
        /// Format this array with one child array per line, and those arrays padded to line up nicely.
        /// </summary>
        private bool FormatTableArrayArray(FormattedNode thisItem)
        {
            if (TableArrayMinimumSimilarity > 100.5)
                return false;

            // Gather stats about our children's item widths, if they're eligible arrays.
            var columnStats = GetArrayStats(thisItem);
            if (columnStats == null)
                return false;

            // Reformat our immediate children using the width info we've computed.  Their children aren't
            // recomputed, so this part isn't recursive.
            foreach (var child in thisItem.Children)
                FormatArrayTableRow(child, columnStats);

            return FormatArrayExpanded(thisItem);
        }

        /// <summary>
        /// Format this array in a single line, with padding to line up with siblings.
        /// </summary>
        private void FormatArrayTableRow(FormattedNode thisItem, ColumnStats[] columnStatsArray)
        {
            _buff.Clear();
            _buff.Append("[ ");

            // Write the elements that actually exist in this array.
            for (var index = 0; index < thisItem.Children.Count; ++index)
            {
                if (index != 0)
                    _buff.Append(_paddedCommaStr);

                var columnStats = columnStatsArray[index];
                if (columnStats.NumericFormatStr != null && !DontJustifyNumbers)
                {
                    _buff.Append(string.Format(CultureInfo.InvariantCulture, columnStats.NumericFormatStr,
                        double.Parse(thisItem.Children[index].Value)));
                }
                else
                {
                    var padSize = columnStatsArray[index].MaxValueSize - thisItem.Children[index].Value.Length;
                    _buff.Append(thisItem.Children[index].Value).Append(' ', padSize);
                }
            }

            // Write padding for elements that exist in siblings but not this array.
            for (var index = thisItem.Children.Count; index < columnStatsArray.Length; ++index)
            {
                var padSize = columnStatsArray[index].MaxValueSize
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

            var lineLength = 2 + (useNestedBracketPadding ? 2 : 0) // outer brackets
                               + thisItem.Children.Count * _paddedColonStr.Length // colons
                               + (thisItem.Children.Count - 1) * _paddedCommaStr.Length // commas
                               + thisItem.Children.Count * 2 // prop quotes
                               + thisItem.Children.Sum(fn => fn.Name.Length) // propnames
                               + thisItem.Children.Sum(fn => fn.Value.Length); // values
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

            thisItem.Value = _buff.ToString();
            thisItem.Format = Format.Inline;
            return true;
        }

        /// <summary>
        /// Format this object with one child object per line, and those objects padded to line up nicely.
        /// </summary>
        private bool FormatTableObjectObject(FormattedNode thisItem)
        {
            if (TableObjectMinimumSimliarity > 100.5)
                return false;

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
            if (TableArrayMinimumSimilarity > 100.5)
                return false;

            // Gather stats about our children's widths, if they're eligible arrays.
            var columnStats = GetArrayStats(thisItem);
            if (columnStats == null)
                return false;

            // Reformat our immediate children using the width info we've computed.  Their children aren't
            // recomputed, so this part isn't recursive.
            foreach (var child in thisItem.Children)
                FormatArrayTableRow(child, columnStats);

            return FormatObjectExpanded(thisItem, true);
        }

        /// <summary>
        /// Format this object in a single line, with padding to line up with siblings.
        /// </summary>
        private void FormatObjectTableRow(FormattedNode thisItem, ColumnStats[] columnStatsArray)
        {
            // Bundle up each property name, value, quotes, colons, etc., or equivalent empty space.
            var highestNonBlankIndex = -1;
            var propSegmentStrings = new string?[columnStatsArray.Length];
            for (var colIndex = 0; colIndex < columnStatsArray.Length; ++colIndex)
            {
                _buff.Clear();
                var columnStats = columnStatsArray[colIndex];
                var propNode = thisItem.Children.FirstOrDefault(fn => fn.Name == columnStats.PropName);
                if (propNode == null)
                {
                    // This object doesn't have this particular property.  Pad it out.
                    var skipLength = 2
                                     + columnStats.PropName.Length
                                     + _paddedColonStr.Length
                                     + columnStats.MaxValueSize;
                    _buff.Append(' ', skipLength);
                }
                else
                {
                    var valuePadLength = columnStats.MaxValueSize - propNode.Value.Length;
                    _buff.Append('"').Append(columnStats.PropName).Append('"').Append(_paddedColonStr);

                    if (columnStats.NumericFormatStr != null && !DontJustifyNumbers)
                        _buff.Append(string.Format(CultureInfo.InvariantCulture, columnStats.NumericFormatStr,
                            double.Parse(propNode.Value)));
                    else
                        _buff.Append(propNode.Value).Append(' ', valuePadLength);
                    highestNonBlankIndex = colIndex;
                }

                propSegmentStrings[colIndex] = _buff.ToString();
            }

            _buff.Clear();
            _buff.Append("{ ");

            // Put them all together with commas in the right places.
            var firstElem = true;
            var needsComma = false;
            for (var segmentIndex = 0; segmentIndex < propSegmentStrings.Length; ++segmentIndex)
            {
                if (needsComma && segmentIndex <= highestNonBlankIndex)
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

                if (AlignExpandedPropertyNames || forceExpandPropNames)
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

        /// <summary>
        /// If the given nodes are all numbers and not too big or small, format them to the same precision and width.
        /// </summary>
        private void JustifyParallelNumbers(IList<FormattedNode> itemList)
        {
            if (itemList.Count < 2 || DontJustifyNumbers)
                return;

            var columnStats = new ColumnStats();
            foreach (var propNode in itemList)
                columnStats.Update(propNode, 0);

            columnStats.MakeNumericFormatString();
            if (columnStats.NumericFormatStr == null)
                return;

            foreach (var propNode in itemList)
                propNode.Value = string.Format(CultureInfo.InvariantCulture, columnStats.NumericFormatStr,
                    double.Parse(propNode.Value));
        }

        /// <summary>
        /// Add the appropriate number of tabs or spaces for the given depth.
        /// </summary>
        private StringBuilder Indent(StringBuilder buff, int depth)
        {
            _buff.Append(PrefixString);
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
        /// their properties, such as max width.  Returns null if they're not eligible.
        /// </summary>
        private ColumnStats[]? GetPropertyStats(FormattedNode thisItem)
        {
            if (thisItem.Children.Count < 2)
                return null;

            // Record every property across all objects, count them, tabulate their order, and find the longest.
            var props = new Dictionary<string, ColumnStats>();
            foreach (var child in thisItem.Children)
            {
                if (child.Kind != JsonValueKind.Object || child.Format != Format.Inline)
                    return null;

                for (var index = 0; index < child.Children.Count; ++index)
                {
                    var propNode = child.Children[index];
                    props.TryGetValue(propNode.Name, out var propStats);
                    if (propStats == null)
                    {
                        propStats = new ColumnStats() {PropName = propNode.Name};
                        props.Add(propStats.PropName, propStats);
                    }

                    propStats.Update(propNode, index);
                }
            }

            // Decide the order of the properties by sorting by the average index.  It's a crude metric,
            // but it should handle the occasional missing property well enough.
            var orderedProps = props.Values
                .OrderBy(ps => (ps.OrderSum / (double) ps.Count))
                .ToArray();

            // Calculate a score based on how many of all possible properties are present.  If the score is too
            // low, these objects are too different to try to line up as a table.
            var score = 100.0 * orderedProps.Sum(cs => cs.Count)
                        / (orderedProps.Length * thisItem.Children.Count);
            if (score < TableObjectMinimumSimliarity)
                return null;

            foreach (var propStats in orderedProps.Where(cs => cs.IsQualifiedNumeric))
                propStats.MakeNumericFormatString();

            // If the formatted lines would be too long, bail out.
            var lineLength = 4                                                          // outer brackets & spaces
                             + 2 * orderedProps.Length                                     // property quotes
                             + orderedProps.Sum(ps => ps.PropName.Length) // prop names
                             + _paddedColonStr.Length * orderedProps.Length                // colons
                             + orderedProps.Sum(ps => ps.MaxValueSize)    // values
                             + _paddedCommaStr.Length * (orderedProps.Length - 1);         // commas
            if (lineLength > MaxInlineLength)
                return null;

            return orderedProps;
        }

        /// <summary>
        /// Check if this node's array children can be formatted as a table, and if so, gather stats like max width.
        /// Returns null if they're not eligible.
        /// </summary>
        private ColumnStats[]? GetArrayStats(FormattedNode thisItem)
        {
            if (thisItem.Children.Count < 2)
                return null;

            var valid = thisItem.Children.All(fn => fn.Kind == JsonValueKind.Array && fn.Format == Format.Inline);
            if (!valid)
                return null;

            var numberOfColumns = thisItem.Children.Max(fn => fn.Children.Count);
            var colStatsArray = new ColumnStats[numberOfColumns];
            for (int i = 0; i < colStatsArray.Length; ++i)
                colStatsArray[i] = new ColumnStats();

            foreach (var rowNode in thisItem.Children)
            {
                for (var index = 0; index < rowNode.Children.Count; ++index)
                    colStatsArray[index].Update(rowNode.Children[index], index);
            }

            // Calculate a score based on how rectangular the arrays are.  If they differ too much in length,
            // it probably doesn't make sense to format them together.
            var similarity = 100.0 * thisItem.Children.Sum(fn => fn.Children.Count)
                             / (thisItem.Children.Count * numberOfColumns);
            if (similarity < TableArrayMinimumSimilarity)
                return null;

            foreach (var colStats in colStatsArray)
                colStats.MakeNumericFormatString();

            // If the formatted lines would be too long, bail out.
            var lineLength = 4                                            // outer brackets
                + colStatsArray.Sum(ps => ps.MaxValueSize)  // values
                + colStatsArray.Length - 1 * _paddedCommaStr.Length;         // commas
            if (lineLength > MaxInlineLength)
                return null;

            return colStatsArray;
        }
    }

    internal enum Format
    {
        Inline,
        InlineTabular,
        MultilineCompact,
        Expanded,
    }

    /// <summary>
    /// Used in figuring out how to format properties/array items as columns in a table format.
    /// </summary>
    internal class ColumnStats
    {
        public string PropName { get; set; } = string.Empty;
        public int OrderSum { get; set; }
        public int Count { get; set; }
        public int MaxValueSize { get; set; }
        public bool IsQualifiedNumeric { get; set; } = true;
        public int CharsBeforeDec { get; set; }
        public int CharsAfterDec { get; set; }
        public string? NumericFormatStr { get; set; }

        /// <summary>
        /// Add stats about this FormattedNode to this PropertyStats.
        /// </summary>
        public void Update(FormattedNode propNode, int index)
        {
            OrderSum += index;
            Count += 1;
            MaxValueSize = Math.Max(MaxValueSize, propNode.Value.Length);
            IsQualifiedNumeric &= (propNode.Kind == JsonValueKind.Number);

            if (!IsQualifiedNumeric)
                return;

            // Gather extra stats about numbers, if appropriate
            var normalizedNum = double.Parse(propNode.Value).ToString(CultureInfo.InvariantCulture);
            IsQualifiedNumeric &= (!normalizedNum.Contains('e') && !normalizedNum.Contains('E'));

            if (!IsQualifiedNumeric)
                return;

            var decIndex = normalizedNum.IndexOf('.');
            if (decIndex < 0)
            {
                CharsBeforeDec = Math.Max(CharsBeforeDec, normalizedNum.Length);
            }
            else
            {
                CharsBeforeDec = Math.Max(CharsBeforeDec, decIndex);
                CharsAfterDec = Math.Max(CharsAfterDec, normalizedNum.Length - decIndex - 1);
            }
        }

        /// <summary>
        /// Create a format string (for string.format) to format this column as a number, if appropriate.
        /// </summary>
        public void MakeNumericFormatString()
        {
            if (!IsQualifiedNumeric)
                return;
            MaxValueSize = CharsBeforeDec + CharsAfterDec + ((CharsAfterDec > 0) ? 1 : 0);
            NumericFormatStr = "{" + $"0,{MaxValueSize}:f{CharsAfterDec}" + "}";
        }
    }

    /// <summary>
    /// Data about a JSON element and how we've formatted it.
    /// </summary>
    internal class FormattedNode
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
}
