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

        private readonly StringBuilder _buff = new StringBuilder();
        private string _eolStr = string.Empty;
        private string _indentStr = string.Empty;
        private string _paddedCommaStr = string.Empty;
        private string _paddedColonStr = String.Empty;

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

        private FormattedNode FormatElement(int depth, JsonElement element)
        {
            var formattedItem = element.ValueKind switch
            {
                JsonValueKind.Array => FormatArray(depth, element),
                JsonValueKind.Object => FormatObject(depth, element),
                _ => FormatSimple(depth, element)
            };

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

            FormatObjectExpanded(thisItem);
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

        private bool FormatObjectExpanded(FormattedNode thisItem)
        {
            _buff.Clear();

            _buff.Append('{').Append(_eolStr);

            var firstItem = true;
            foreach (var prop in thisItem.Children)
            {
                if (!firstItem)
                    _buff.Append(',').Append(_eolStr);
                Indent(_buff, prop.Depth).Append('"').Append(prop.Name).Append('"')
                    .Append(_paddedColonStr).Append(prop.Value);
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

        private void Cleanup(FormattedNode thisItem)
        {
            foreach (var child in thisItem.Children)
                child.Children = Array.Empty<FormattedNode>();
        }

        private enum Format
        {
            Inline,
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
    }
}
