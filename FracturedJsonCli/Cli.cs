using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using FracturedJson;
using Mono.Options;

namespace FracturedJsonCli
{
    /// <summary>
    /// Commandline app to format JSON using FracturedJson.Formatter.  Output is to standard out.
    /// Input is from either standard in, or from a file if using the --file switch.
    /// </summary>
    internal static class Cli
    {
        internal static int Main(string[] args)
        {
            try
            {
                var options = new FracturedJsonOptions()
                {
                    MaxInlineLength = int.MaxValue,
                };

                var showHelp = false;
                string? fileName = null;
                var noPadding = false;
                var allowComments = false;
                var speedTest = false;

                var cliOpts = new OptionSet()
                {
                    { "a|allow", "allow comments and trailing commas", v => allowComments = (v != null) },
                    { "c|complexity=", "maximum inline complexity", (int n) => options.MaxInlineComplexity = n },
                    { "f|file=", "input from file instead of stdin", s => fileName = s },
                    { "h|help", "show this help info and exit", v => showHelp = (v != null) },
                    { "j|no-justify", "don't justify parallel numbers", v => options.DontJustifyNumbers = true },
                    { "l|length=", "maximum total line length", (int n) => options.MaxTotalLineLength = n },
                    {
                        "m|multiline=",
                        "maximum multi-line array complexity",
                        (int n) => options.MaxCompactArrayComplexity = n
                    },
                    { "p|no-padding", "don't include padding spaces", v => noPadding = (v != null) },
                    {
                        "s|space=",
                        "use this many spaces per indent level",
                        (int n) => options.IndentSpaces = n
                    },
                    { "r|row=", "maximum table row complexity", (int n) => options.MaxTableRowComplexity = n },
                    { "t|tab", "use tabs for indentation", _ => options.UseTabToIndent = true },
                    { "u|unix", "use Unix line endings (LF)", v => options.JsonEolStyle = EolStyle.Lf },
                    { "w|windows", "use Windows line endings (CRLF)", v => options.JsonEolStyle = EolStyle.Crlf },
                    { "z|speed-test", "write timer data instead of JSON output", v => speedTest = (v != null) },
                };

                cliOpts.Parse(args);

                if (showHelp)
                {
                    ShowHelp(cliOpts);
                    return 0;
                }

                if (noPadding)
                {
                    options.ColonPadding = false;
                    options.CommaPadding = false;
                    options.CommentPadding = false;
                    options.NestedBracketPadding = false;
                }

                if (allowComments)
                {
                    options.CommentPolicy = CommentPolicy.Preserve;
                    options.AllowTrailingCommas = true;
                    options.PreserveBlankLines = true;
                }

                string inputText;
                if (fileName != null)
                {
                    inputText = File.ReadAllText(fileName);
                }
                else if (Console.IsInputRedirected)
                {
                    inputText = Console.In.ReadToEnd();
                }
                else
                {
                    ShowHelp(cliOpts);
                    return 1;
                }

                var timer = Stopwatch.StartNew();

                var formatter = new Formatter() { Options = options };
                var formattedDoc = formatter.Reformat(inputText, 0);

                timer.Stop();

                if (speedTest)
                    Console.WriteLine($"Processing time: {timer.Elapsed.TotalSeconds} sec");
                else
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
