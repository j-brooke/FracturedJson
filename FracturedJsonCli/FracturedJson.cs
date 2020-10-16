using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System;

namespace FracturedJsonCli
{
    public enum FracturedEolStyle
    {
        Default,
        Crlf,
        Lf,
    }
    
    /// <summary>
    /// Class that outputs JSON formatted in a compact user-readable way.  Arrays and objects that are neither
    /// too complex nor too long are written as single lines.  More complex elements are written to multiple
    /// lines, indented.
    /// </summary>
    public class FracturedJson
    {
        /// <summary>
        /// Dictates what sort of line endings to use.
        /// </summary>
        public FracturedEolStyle EolStyle { get; set; } = FracturedEolStyle.Default;
        
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
        /// If an array contains only simple elements and MultiInlineSimpleArrays is true, the array can
        /// span multiple lines with multiple items per line.  Otherwise, arrays that are too long to fit
        /// on a single line are broken out one item per line.
        /// </summary>
        public bool MultiInlineSimpleArrays { get; set; } = true;

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
            return FormatElement(0, document.RootElement, out _);
        }

        private const string _legalWhitespace = " \r\n\t";
        private string _indentString = "    ";
        private string _colonPaddingStr = string.Empty;
        private string _commaPaddingStr = string.Empty;
        private string _eolStr = string.Empty;

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
                    return FormatSimple(element, out complexity);
            }
        }

        private string FormatArray(int depth, JsonElement array, out int complexity)
        {
            var maxChildComplexity = 0;

            // Format all array items, and pay attention to how complex they are.
            var items = array.EnumerateArray()
                .Select(je => {
                    var elemStr = FormatElement(depth+1, je, out var childComplexity);
                    maxChildComplexity = Math.Max(maxChildComplexity, childComplexity);
                    return elemStr;
                })
                .ToList();

            // Treat an empty array as a primitive: zero complexity.
            if (items.Count==0)
            {
                complexity = 0;
                return "[]";
            }

            complexity = maxChildComplexity + 1;

            // Try formatting this array as a single line, if none of the children are too complex,
            // and the total length isn't excessive.
            var lengthEstimate = items.Sum(valStr => valStr.Length+1);
            if (maxChildComplexity<MaxInlineComplexity && lengthEstimate<=MaxInlineLength)
            {
                var inlineStr = FormatArrayInline(items, maxChildComplexity);
                if (inlineStr.Length<=MaxInlineLength)
                    return inlineStr;
            }

            // We couldn't do a single line.  But if all child elements are simple and we're allowed, write 
            // them on a couple lines, multiple items per line.
            if (maxChildComplexity==0 && MultiInlineSimpleArrays)
            {
                var multiInlineStr = FormatArrayMultiInlineSimple(depth, items);
                return multiInlineStr;
            }

            // If we've gotten this far, we have to write it as a complex object.  Each child element gets its own
            // line (or more).
            var buff = new StringBuilder(lengthEstimate * 3 / 2);
            buff.Append('[').Append(_eolStr);
            var firstElem = true;
            foreach (var item in items)
            {
                if (!firstElem)
                    buff.Append(',').Append(_eolStr);
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
            var buff = new StringBuilder(MaxInlineLength);
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
            var sumItemLengths = itemList.Sum(s => s.Length);
            var buff = new StringBuilder(sumItemLengths * 3 / 2);
            buff.Append('[').Append(_eolStr);
            Indent(depth+1, buff);

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
                    Indent(depth+1, buff);
                    lineLengthSoFar = 0;
                }

                buff.Append(itemList[itemIndex]);
                if (notLastItem)
                    buff.Append(',').Append(_commaPaddingStr);

                itemIndex += 1;
                lineLengthSoFar += segmentLength;
            }

            buff.AppendLine();
            Indent(depth, buff);
            buff.Append(']');

            return buff.ToString();
        }

        private string FormatObject(int depth, JsonElement obj, out int complexity)
        {
            var maxChildComplexity = 0;

            // Format all child property values.
            var keyValPairs = obj.EnumerateObject()
                .Select( jp => {
                    var valStr = FormatElement(depth+1, jp.Value, out var valComplexity);
                    maxChildComplexity = Math.Max(maxChildComplexity, valComplexity);
                    return new FormattedProperty(jp.Name, valStr);
                })
                .ToList();

            // Treat an empty array as a primitive: zero complexity.
            if (keyValPairs.Count==0)
            {
                complexity = 0;
                return "{}";
            }

            complexity = maxChildComplexity + 1;

            // Try formatting this object in a single line, if none of the children are too complicated, and 
            // the total length isn't too long.
            var lengthEstimate = keyValPairs.Sum(kvp => kvp.Name.Length + kvp.Value.Length + 4);
            if (maxChildComplexity<MaxInlineComplexity && lengthEstimate<=MaxInlineLength)
            {
                var inlineStr = FormatObjectInline(keyValPairs, maxChildComplexity);
                if (inlineStr.Length<=MaxInlineLength)
                    return inlineStr;
            }

            // If we've gotten this far, we have to write it as a complex object.  Each child property gets its
            // own line, or more.
            var buff = new StringBuilder();
            buff.Append('{').Append(_eolStr);
            var firstItem = true;
            foreach (var prop in keyValPairs)
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

        private string FormatSimple(JsonElement simpleElem, out int complexity)
        {
            // Return the existing text of the item.  Since it's not an array or object, there won't be any ambiguous
            // whitespace in it.
            complexity = 0;
            return simpleElem.GetRawText();
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

            _eolStr = EolStyle switch
            {
                FracturedEolStyle.Crlf => "\r\n",
                FracturedEolStyle.Lf => "\n",
                _ => Environment.NewLine,
            };
        }

        // Tuples are for the weak-minded.
        private class FormattedProperty
        {
            public string Name { get; }
            public string Value { get; }

            public FormattedProperty(string name, string value)
            {
                Name = name;
                Value = value;
            }
        }
    }
}