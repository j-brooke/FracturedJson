using System;
using System.Collections.Generic;
using FracturedJson.Tokenizer;

namespace FracturedJson.V3;

public class Formatter
{
    public FracturedJsonOptions Options { get; set; } = new();
    public Func<string,int> StringLengthFunc = (s) => s.Length;

    public string Reformat(IEnumerable<char> jsonText)
    {
        throw new NotImplementedException();
    }

    private JsonItem ParseItem(IEnumerator<JsonToken> enumerator)
    {
        throw new NotImplementedException();
    }
}
