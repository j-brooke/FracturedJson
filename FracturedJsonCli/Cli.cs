using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Mono.Options;
using FracturedJson;

namespace FracturedJsonCli
{
    /// <summary>
    /// Commandline app to format JSON using FracturedJson.Formatter.  Output is to standard out.  Input
    /// is expected from standard in, or you can use --file and give it a file name.
    /// </summary>
    internal static class Cli
    {
        internal static int Main(string[] args)
        {
            try
            {
                var formatter = new Formatter();

                bool showHelp = false;
                string? fileName = null;
                bool noPadding = false;

                var cliOpts = new OptionSet()
                {
                    {
                        "a|array=",
                        "table array minimum similarity",
                        (double d) => formatter.TableArrayMinimumSimilarity = d
                    },
                    {"c|complexity=", "maximum inline complexity", (int n) => formatter.MaxInlineComplexity = n},
                    {"e|expand=", "always-expand depth", (int n) => formatter.AlwaysExpandDepth = n},
                    {"f|file=", "input from file instead of stdin", s => fileName = s},
                    {"h|help", "show this help info and exit", v => showHelp = (v != null)},
                    {"j|no-justify", "don't justify parallel numbers", v => formatter.DontJustifyNumbers = true},
                    {"l|length=", "maximum inline length", (int n) => formatter.MaxInlineLength = n},
                    {
                        "m|multiline=",
                        "maximum multi-line array complexity",
                        (int n) => formatter.MaxCompactArrayComplexity = n
                    },
                    {
                        "o|object=",
                        "table object minimum similarity",
                        (double d) => formatter.TableObjectMinimumSimilarity = d
                    },
                    {"p|no-padding", "don't include padding spaces", v => noPadding = (v != null)},
                    {
                        "s|space=",
                        "use this many spaces per indent level",
                        (int n) => formatter.IndentSpaces = n
                    },
                    {"t|tab", "use tabs for indentation", _ => formatter.UseTabToIndent = true},
                    {"u|unix", "use Unix line endings (LF)", v => formatter.JsonEolStyle = EolStyle.Lf},
                    {"w|windows", "use Windows line endings (CRLF)", v => formatter.JsonEolStyle = EolStyle.Crlf},
                };

                cliOpts.Parse(args);

                if (showHelp)
                {
                    ShowHelp(cliOpts);
                    return 0;
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
                    return 1;
                }

                var formattedDoc = formatter.Serialize(jsonDocument);
                Console.WriteLine(formattedDoc);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static void ShowHelp(OptionSet cliOptions)
        {
            var lines = new[]
            {
                $"Version {Assembly.GetExecutingAssembly().GetName().Version}",
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