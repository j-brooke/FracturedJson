using System;
using System.IO;
using System.Text.Json;

namespace FracturedJsonCli
{
    /// <summary>
    /// Commandline app to format JSON using FracturedJson.  The first argument should be the path of
    /// a JSON file.  Output is to standard out.
    /// </summary>
    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length<1)
            {
                Console.WriteLine("I came here for an argument");
                return;
            }

            try
            {
                var docOpts = new JsonDocumentOptions()
                {
                    CommentHandling = JsonCommentHandling.Skip
                };

                using var stream = File.OpenRead(args[0]);
                var doc = JsonDocument.Parse(stream, docOpts);

                var formatter = new FracturedJson()
                {
                    MaxInlineComplexity=2,
                    MaxInlineLength=90,
                    NestedBracketPadding=true,
                    ColonPadding=true,
                    CommaPadding=true,
                    MultiInlineSimpleArrays=true,
                };
                var formattedDoc = formatter.Serialize(doc);

                Console.WriteLine(formattedDoc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
        }
    }
}