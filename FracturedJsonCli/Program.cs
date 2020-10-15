using System;
using System.IO;
using System.Text.Json;

namespace FracturedJsonCli
{
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
                    ColonPadding=false,
                    CommaPadding=true,
                    MultiInlineSimpleArrays=true,
                };
                var formattedDoc = formatter.Write(doc);

                Console.WriteLine(formattedDoc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
        }
    }
}