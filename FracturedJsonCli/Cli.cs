using System;
using System.IO;
using System.Text.Json;
using Mono.Options;

namespace FracturedJsonCli
{
    /// <summary>
    /// Commandline app to format JSON using FracturedJson.  Output is to standard out.  Input
    /// is expected from standard in, or you can use --file and give it a file name.
    /// </summary>
    internal static class Cli
    {
        internal static void Main(string[] args)
        {
            try
            {
                var formatter = new FracturedJson();

                bool showHelp = false;
                string? fileName = null;
                bool noPadding = false;

                var cliOpts = new OptionSet()
                {
                    { "h|help", "show this help info and exit", v => showHelp = (v!=null) },
                    { "f|file=", "input from file instead of stdin", s => fileName = s },
                    { "p|no-padding", "don't include padding spaces", v => noPadding = (v!=null) },
                    { "c|complexity=", "maximum inline complexity", (int n) => formatter.MaxInlineComplexity = n },
                    { "m|multiline=", "maximum multi-line array complexity", (int n) => formatter.MaxCompactArrayComplexity = n },
                    { "l|length=", "maximum inline length", (int n) => formatter.MaxInlineLength = n },
                    { "t|tab", "use tabs for indentation", v => formatter.IndentString = "\t" },
                    { "s|space=", "use this many spaces", (int n) => formatter.IndentString = new string(' ', n) },
                    { "w|windows", "use Windows line endings (CRLF)", v => formatter.EolStyle = FracturedEolStyle.Crlf },
                    { "u|unix", "use Unix line endings (LF)", v => formatter.EolStyle = FracturedEolStyle.Lf },
                };

                cliOpts.Parse(args);

                if (showHelp)
                {
                    ShowHelp(cliOpts);
                    return;
                }

                if (noPadding)
                {
                    formatter.ColonPadding = false;
                    formatter.CommaPadding = false;
                    formatter.NestedBracketPadding = false;
                }

                JsonDocument jsonDocument;
                var docOpts = new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip };

                if (fileName != null)
                {
                    using var stream = File.OpenRead(fileName);
                    jsonDocument = JsonDocument.Parse(stream, docOpts);
                }
                else if (Console.IsInputRedirected)
                {
                    var inputData = Console.In.ReadToEnd();
                    jsonDocument = JsonDocument.Parse(inputData, docOpts);
                }
                else
                {
                    ShowHelp(cliOpts);
                    return;
                }

                var formattedDoc = formatter.Serialize(jsonDocument);
                Console.WriteLine(formattedDoc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
        }

        private static void ShowHelp(OptionSet cliOptions)
        {
            var lines = new[]
            {
                "Usage:",
                "  FracturedJsonCli [OPTIONS]",
                "",
                "Formats JSON to stdout from stdin, or from a file if --file is used.",
                "",
                "Options:",
            };

            foreach (var line in lines)
                Console.Out.WriteLine(line);

            cliOptions.WriteOptionDescriptions(Console.Out);
        }
    }
}