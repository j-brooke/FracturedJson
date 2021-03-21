/*
 * FracturedJson
 * FracturedJson is a library for formatting JSON documents in a human-readable but fairly compact way.
 *
 * Copyright (c) 2020 Jesse Brooke
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
        /// If true, numbers in lists of nothing but numbers are right-justified and padded to the same length.
        /// </summary>
        public bool JustifyNumberLists { get; set; }

        /// <summary>
        /// Depth at which lists/objects are always fully expanded, regardless of other settings.
        /// -1 = none; 0 = root node only; 1 = root node and its children.
        /// </summary>
        public int AlwaysExpandDepth { get; set; } = -1;

        /// <summary>
        /// String composed of spaces and/or tabs specifying one unit of indentation.  Default is 4 spaces.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// If the string contains characters other than space, tab, CR, or LF.
        /// </exception>
        public string IndentString {
            get { return _indentString; }
            set
            {
                if (value.Any(c => !_legalWhitespace.Contains(c)))
                    throw new ArgumentException("IndentString must be legal whitespace");
                _indentString = value;
            }
        }

        /// <summary>
        /// Returns the JSON documented formatted as a string, with simpler collections written in single
        /// lines where possible.
        /// </summary>
        public string Serialize(JsonDocument document)
        {
            SetPaddingStrings();
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
        public string Serialize<T>(T obj, JsonSerializerOptions? options = null)
        {
            options ??= new JsonSerializerOptions() {Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
            var doc = JsonSerializer.Serialize(obj, options);
            return Serialize(doc);
        }

        private const string _legalWhitespace = " \r\n\t";
        private string _indentString = "    ";
        private string _colonPaddingStr = string.Empty;
        private string _commaPaddingStr = string.Empty;
        private string _eolStr = string.Empty;

        private FormattedElem FormatElement(int depth, JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => FormatArray(depth, element),
                JsonValueKind.Object => FormatObject(depth, element),
                _ => FormatSimple(element)
            };
        }

        private FormattedElem FormatSimple(JsonElement element)
        {
            // Return the existing text of the item.  Since it's not an array or object, there won't be any ambiguous
            // whitespace in it.
            return new FormattedElem(element.GetRawText(), 0, element.ValueKind);
        }

        private FormattedElem FormatArray(int depth, JsonElement array)
        {
            var items = array.EnumerateArray()
                .Select(child => FormatElement(depth + 1, child))
                .ToArray();

            // Treat an empty array as a primitive: zero complexity.
            if (items.Length == 0)
                return new FormattedElem("[]", 0, JsonValueKind.Array);

            var lengthEstimate = items.Sum(child => (long) child.Value.Length);
            if (lengthEstimate>int.MaxValue)
                throw new ArgumentException("The JSON document is too large to be formatted");

            var maxChildComplexity = items.Max(child => child.Complexity);
            var justifyLength = (JustifyNumberLists && items.All(child => child.Kind == JsonValueKind.Number))
                ? items.Max(child => child.Value.Length)
                : 0;

            var alwaysExpandThis = depth <= AlwaysExpandDepth;

            // Try formatting this array as a single line, if none of the children are too complex,
            // and the total length isn't excessive.
            if (!alwaysExpandThis && maxChildComplexity < MaxInlineComplexity && lengthEstimate <= MaxInlineLength)
            {
                var inlineElem = FormatArrayInline(items, maxChildComplexity, justifyLength);
                if (inlineElem.Value.Length <= MaxInlineLength)
                    return inlineElem;
            }

            // We couldn't do a single line.  But if all child elements are simple and we're allowed, write
            // them on a couple lines, multiple items per line.
            if (!alwaysExpandThis && maxChildComplexity < MaxCompactArrayComplexity)
                return FormatArrayMultiInlineSimple(depth, items, maxChildComplexity, justifyLength);


            // If we've gotten this far, we have to write it as a complex object.  Each child element gets its own
            // line (or more).
            var bufferCapacity = Math.Min(16, (int) Math.Min(lengthEstimate * 3 / 2, int.MaxValue));
            var buff = new StringBuilder(bufferCapacity);
            buff.Append('[').Append(_eolStr);
            var firstElem = true;
            foreach (var item in items)
            {
                if (!firstElem)
                    buff.Append(',').Append(_eolStr);
                Indent(depth+1, buff);
                if (justifyLength>0)
                    buff.Append(' ', justifyLength-item.Value.Length);
                buff.Append(item.Value);
                firstElem = false;
            }

            buff.AppendLine();
            Indent(depth, buff);
            buff.Append("]");

            return new FormattedElem(buff.ToString(), maxChildComplexity + 1, JsonValueKind.Array);
        }

        private FormattedElem FormatArrayInline(IList<FormattedElem> items, int maxChildComplexity, int justifyLength)
        {
            var buff = new StringBuilder(MaxInlineLength);
            buff.Append("[");

            if (NestedBracketPadding && maxChildComplexity>0)
                buff.Append(' ');

            var firstElem = true;
            foreach (var elem in items)
            {
                if (!firstElem)
                    buff.Append(",").Append(_commaPaddingStr);
                if (justifyLength>0)
                    buff.Append(' ', justifyLength-elem.Value.Length);
                buff.Append(elem.Value);
                firstElem = false;
            }

            if (NestedBracketPadding && maxChildComplexity>0)
                buff.Append(' ');

            buff.Append("]");
            return new FormattedElem(buff.ToString(), maxChildComplexity + 1, JsonValueKind.Array);
        }

        private FormattedElem FormatArrayMultiInlineSimple(int depth, IList<FormattedElem> items,
            int maxChildComplexity, int justifyLength)
        {
            var sumItemLengths = items.Sum(elem => elem.Value.Length);
            var buff = new StringBuilder(sumItemLengths * 3 / 2);
            buff.Append('[').Append(_eolStr);
            Indent(depth+1, buff);

            var lineLengthSoFar = 0;
            var itemIndex = 0;
            while (itemIndex<items.Count)
            {
                bool notLastItem = itemIndex < items.Count - 1;

                var itemLength = Math.Max(justifyLength, items[itemIndex].Value.Length);
                var segmentLength = itemLength + ((notLastItem)? 1 + _commaPaddingStr.Length : 0);
                if (lineLengthSoFar + segmentLength > MaxInlineLength && lineLengthSoFar>0)
                {
                    buff.AppendLine();
                    Indent(depth+1, buff);
                    lineLengthSoFar = 0;
                }

                if (justifyLength>0)
                    buff.Append(' ', justifyLength-items[itemIndex].Value.Length);
                buff.Append(items[itemIndex].Value);
                if (notLastItem)
                    buff.Append(',').Append(_commaPaddingStr);

                itemIndex += 1;
                lineLengthSoFar += segmentLength;
            }

            buff.AppendLine();
            Indent(depth, buff);
            buff.Append(']');

            return new FormattedElem(buff.ToString(), maxChildComplexity + 1, JsonValueKind.Array);
        }

        private FormattedElem FormatObject(int depth, JsonElement obj)
        {
            var maxChildComplexity = 0;
            long lengthEstimate = 0;
            var properties = new List<FormattedElem>();

            // Format all child property values.
            foreach (var jsonProp in obj.EnumerateObject())
            {
                var item = FormatElement(depth + 1, jsonProp.Value);
                item.Name = jsonProp.Name;
                properties.Add(item);
                maxChildComplexity = Math.Max(maxChildComplexity, item.Complexity);
                lengthEstimate += item.Value.Length + jsonProp.Name.Length + 4;
            }

            // Treat an empty array as a primitive: zero complexity.
            if (properties.Count == 0)
                return new FormattedElem("{}", 0, JsonValueKind.Object);

            if (lengthEstimate>int.MaxValue)
                throw new ArgumentException("The JSON document is too large to be formatted");

            var alwaysExpandThis = depth <= AlwaysExpandDepth;

            // Try formatting this object in a single line, if none of the children are too complicated, and
            // the total length isn't too long.
            if (!alwaysExpandThis && maxChildComplexity<MaxInlineComplexity && lengthEstimate<=MaxInlineLength)
            {
                var inlineStr = FormatObjectInline(properties, maxChildComplexity);
                if (inlineStr.Value.Length<=MaxInlineLength)
                    return inlineStr;
            }

            // If we've gotten this far, we have to write it as a complex object.  Each child property gets its
            // own line, or more.
            var bufferCapacity = Math.Min(16, (int) Math.Min(lengthEstimate * 3 / 2, int.MaxValue));
            var buff = new StringBuilder(bufferCapacity);

            buff.Append('{').Append(_eolStr);
            var firstItem = true;
            foreach (var prop in properties)
            {
                if (!firstItem)
                    buff.Append(',').Append(_eolStr);
                Indent(depth+1, buff);
                buff.Append('"').Append(prop.Name).Append('"').Append(':').Append(_colonPaddingStr);

                buff.Append(prop.Value);
                firstItem = false;
            }

            buff.AppendLine();
            Indent(depth, buff);
            buff.Append("}");

            return new FormattedElem(buff.ToString(), maxChildComplexity + 1, JsonValueKind.Object);
        }

        private FormattedElem FormatObjectInline(IList<FormattedElem> items, int maxChildComplexity)
        {
            var buff = new StringBuilder();
            buff.Append("{");

            if (NestedBracketPadding && maxChildComplexity>0)
                buff.Append(' ');

            var firstElem = true;
            foreach (var prop in items)
            {
                if (!firstElem)
                    buff.Append(",").Append(_commaPaddingStr);
                buff.Append('"').Append(prop.Name).Append('"').Append(':').Append(_colonPaddingStr);

                buff.Append(prop.Value);
                firstElem = false;
            }

            if (NestedBracketPadding && maxChildComplexity>0)
                buff.Append(' ');

            buff.Append('}');
            return new FormattedElem(buff.ToString(), maxChildComplexity + 1, JsonValueKind.Object);
        }

        private void Indent(int depth, StringBuilder buff)
        {
            for (var i=0; i<depth; ++i)
                buff.Append(IndentString);
        }

        /// <summary>
        /// Initialize a couple private fields based on our properties.
        /// </summary>
        private void SetPaddingStrings()
        {
            _colonPaddingStr = (ColonPadding)? " " : "";
            _commaPaddingStr = (CommaPadding)? " " : "";

            _eolStr = JsonEolStyle switch
            {
                EolStyle.Crlf => "\r\n",
                EolStyle.Lf => "\n",
                _ => Environment.NewLine,
            };
        }

        private class FormattedElem
        {
            public string Name { get; set; } = String.Empty;
            public string Value { get; }
            public int Complexity { get; }
            public JsonValueKind Kind { get; }

            public FormattedElem(string value, int complexity, JsonValueKind kind)
            {
                Value = value;
                Complexity = complexity;
                Kind = kind;
            }
        }
    }
}
