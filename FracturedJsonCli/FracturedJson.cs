using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System;

namespace FracturedJsonCli
{
    public class FracturedJson
    {
        /// <summary>
        /// Maximum length of a complex element on a single line.  This includes only
        /// the data for the inlined element, not indentation or leading property names.
        /// </summary>
        public int MaxInlineLength { get; set; } = 100;

        /// <summary>
        /// Maximum nesting level that can be displayed on a single line.  A primitive type or an empty
        /// array or object has a complexity of 0.  An object or array has a complexity of 1 greater than
        /// its most complex child.
        /// </summary>
        public int MaxInlineComplexity { get; set; } = 2;

        /// <summary>
        /// </summary>
        public bool NestedBracketPadding { get; set; } = true;

        /// <summary>
        /// </summary>
        public bool ColonPadding { get; set; } = true;

        /// <summary>
        /// </summary>
        public bool CommaPadding { get; set; } = true;

        /// <summary>
        /// </summary>
        public bool MultiInlineSimpleArrays { get; set; } = true;

        public string IndentString {
            get { return _indentString; }
            set
            {
                if (value.Any(c => !_legalWhitespace.Contains(c)))
                    throw new ArgumentException("IndentString must be legal whitespace");
                _indentString = value;
            }
        }

        public string Write(JsonDocument document)
        {
            SetPaddingStrings();
            return FormatElement(0, document.RootElement, out _);
        }

        private string _legalWhitespace = " \r\n\t";
        private string _indentString = "    ";
        private string _colonPaddingStr = string.Empty;
        private string _commaPaddingStr = string.Empty;

        /// <summary>
        /// Return the element as a formatted string, recursively.  The string doesn't have any leading or
        /// trailing whitespace, but if it's an array/object, it might have internal newlines and indentation.
        /// The assumption is that whatever contains this element will take care of positioning the start.
        /// </summary>
        private string FormatElement(int depth, JsonElement element, out int complexity)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    return FormatArray(depth, element, out complexity);
                case JsonValueKind.Object:
                    return FormatObject(depth, element, out complexity);
                default:
                    return FormatSimple(depth, element, out complexity);
            }
        }

        private string FormatArray(int depth, JsonElement array, out int complexity)
        {
            var maxChildComplexity = 0;

            var items = array.EnumerateArray()
                .Select(je => {
                    var elemStr = FormatElement(depth+1, je, out int complexity);
                    maxChildComplexity = Math.Max(maxChildComplexity, complexity);
                    return elemStr;
                })
                .ToList();

            if (items.Count==0)
            {
                complexity = 0;
                return "[]";
            }

            complexity = maxChildComplexity + 1;

            var lengthEstimate = items.Sum(valStr => valStr.Length+1);
            if (maxChildComplexity<MaxInlineComplexity && lengthEstimate<=MaxInlineLength)
            {
                var inlineStr = FormatArrayInline(items, maxChildComplexity);
                if (inlineStr.Length<=MaxInlineLength)
                    return inlineStr;
            }

            if (maxChildComplexity==0 && MultiInlineSimpleArrays)
            {
                var multiInlineStr = FormatArrayMultiInlineSimple(depth+1, items);
                return multiInlineStr;
            }

            // If we've gotten this far, we have to write it as a complex object.
            var buff = new StringBuilder();
            buff.AppendLine("[");
            var firstElem = true;
            foreach (var item in items)
            {
                if (!firstElem)
                    buff.AppendLine(",");
                Indent(depth+1, buff);
                buff.Append(item);
                firstElem = false;
            }

            buff.AppendLine();
            Indent(depth, buff);
            buff.Append("]");

            return buff.ToString();
        }

        private string FormatArrayInline(IList<string> itemList, int maxChildComplexity)
        {
            var buff = new StringBuilder();
            buff.Append("[");

            if (NestedBracketPadding && maxChildComplexity>0)
                buff.Append(' ');

            var firstElem = true;
            foreach (var itemStr in itemList)
            {
                if (!firstElem)
                    buff.Append(",").Append(_commaPaddingStr);
                buff.Append(itemStr);
                firstElem = false;
            }

            if (NestedBracketPadding && maxChildComplexity>0)
                buff.Append(' ');

            buff.Append("]");

            return buff.ToString();
        }

        private string FormatArrayMultiInlineSimple(int depth, IList<string> itemList)
        {
            var buff = new StringBuilder();
            buff.AppendLine("[");
            Indent(depth, buff);

            var lineLengthSoFar = 0;
            var itemIndex = 0;
            while (itemIndex<itemList.Count)
            {
                bool notLastItem = itemIndex <itemList.Count-1;

                var segmentLength = itemList[itemIndex].Length
                    + ((notLastItem)? 1 + _commaPaddingStr.Length : 0);
                if (lineLengthSoFar + segmentLength > MaxInlineLength && lineLengthSoFar>0)
                {
                    buff.AppendLine();
                    Indent(depth, buff);
                    lineLengthSoFar = 0;
                }

                buff.Append(itemList[itemIndex]);
                if (notLastItem)
                    buff.Append(',').Append(_commaPaddingStr);

                itemIndex += 1;
                lineLengthSoFar += segmentLength;
            }

            buff.AppendLine();
            Indent(depth-1, buff);
            buff.Append(']');

            return buff.ToString();
        }

        private string FormatObject(int depth, JsonElement obj, out int complexity)
        {
            var keyValPairs = obj.EnumerateObject()
                .Select( jp => {
                    var valStr = FormatElement(depth+1, jp.Value, out var valComplexity);
                    return new FormattedProperty(jp.Name, valStr, valComplexity);
                })
                .ToList();

            if (keyValPairs.Count==0)
            {
                complexity = 0;
                return "{}";
            }

            var maxChildComplexity = keyValPairs.Max(kvp => kvp.Complexity);
            complexity = maxChildComplexity + 1;

            var lengthEstimate = keyValPairs.Sum(kvp => kvp.Name.Length + kvp.Value.Length + 4);
            if (maxChildComplexity<MaxInlineComplexity && lengthEstimate<=MaxInlineLength)
            {
                var inlineStr = FormatObjectInline(keyValPairs, maxChildComplexity);
                if (inlineStr.Length<=MaxInlineLength)
                    return inlineStr;
            }

            // If we've gotten this far, we have to write it as a complex object.
            var buff = new StringBuilder();
            buff.AppendLine("{");
            var firstItem = true;
            foreach (var prop in keyValPairs)
            {
                if (!firstItem)
                    buff.AppendLine(",");
                Indent(depth+1, buff);
                buff.Append('"').Append(prop.Name).Append('"').Append(':').Append(_colonPaddingStr);

                buff.Append(prop.Value);
                firstItem = false;
            }

            buff.AppendLine();
            Indent(depth, buff);
            buff.Append("}");

            return buff.ToString();
        }

        private string FormatObjectInline(IList<FormattedProperty> propsList, int maxChildComplexity)
        {
            var buff = new StringBuilder();
            buff.Append("{");

            if (NestedBracketPadding && maxChildComplexity>0)
                buff.Append(' ');

            var firstElem = true;
            foreach (var prop in propsList)
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
            return buff.ToString();
        }

        private string FormatSimple(int depth, JsonElement simpleElem, out int complexity)
        {
            complexity = 0;
            return simpleElem.GetRawText();
        }

        private void Indent(int depth, StringBuilder buff)
        {
            for (var i=0; i<depth; ++i)
                buff.Append(IndentString);
        }

        private void SetPaddingStrings()
        {
            _colonPaddingStr = (ColonPadding)? " " : "";
            _commaPaddingStr = (CommaPadding)? " " : "";
        }

        private class FormattedProperty
        {
            public string Name { get; }
            public string Value { get; }
            public int Complexity { get; }

            public FormattedProperty(string name, string value, int complexity)
            {
                Name = name;
                Value = value;
                Complexity = complexity;
            }
        }
    }
}