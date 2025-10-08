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
                var options = FracturedJsonOptions.Recommended();

                var showHelp = false;
                string? fileName = null;
                var noPadding = false;
                var allowComments = false;
                var speedTest = false;
                string? outFileName = null;
                var minify = false;

                var cliOpts = new OptionSet()
                {
                    { "a|allow", "allow comments and trailing commas", _ => allowComments = true },
                    { "c|complexity=", "maximum inline complexity", (int n) => options.MaxInlineComplexity = n },
                    { "d|diff=", "max name length diff", (int n) => options.MaxAlignPropsPadding = n},
                    { "e|expand=", "always-expand depth", (int n) => options.AlwaysExpandDepth = n },
                    { "f|file=", "input from file instead of stdin", s => fileName = s },
                    { "h|help", "show this help info and exit", _ => showHelp = true },
                    {
                        "j|justify=", "number list justification [l,r,d,n]", s =>
                            options.NumberListAlignment = s.ToUpper() switch
                        {
                            "L" or "LEFT" => NumberListAlignment.Left,
                            "R" or "RIGHT" => NumberListAlignment.Right,
                            "D" or "DECIMAL" => NumberListAlignment.Decimal,
                            "N" or "NORMALIZE" => NumberListAlignment.Normalize,
                            _ => NumberListAlignment.Left,
                        }
                    },
                    {
                        "k|comma=", "table comma placement [b,a,n]", s =>
                            options.TableCommaPlacement = s.ToUpper() switch
                            {
                                "B" or "BEFORE" => TableCommaPlacement.BeforePadding,
                                "A" or "AFTER" => TableCommaPlacement.AfterPadding,
                                "N" or "NUMBER" => TableCommaPlacement.BeforePaddingExceptNumbers,
                                _ => TableCommaPlacement.AfterPadding,
                            }
                    },
                    {
                        "l|length=",
                        "maximum total line length when inlining",
                        (int n) => options.MaxTotalLineLength = n
                    },
                    {
                        "m|multiline=",
                        "maximum multi-line array complexity",
                        (int n) => options.MaxCompactArrayComplexity = n
                    },
                    { "o|outfile=", "write output to file", s => outFileName = s },
                    { "p|no-padding", "don't include padding spaces", _ => noPadding = true },
                    {
                        "s|space=",
                        "use this many spaces per indent level",
                        (int n) => options.IndentSpaces = n
                    },
                    { "r|row=", "maximum table row complexity", (int n) => options.MaxTableRowComplexity = n },
                    { "t|tab", "use tabs for indentation", _ => options.UseTabToIndent = true },
                    { "u|unix", "use Unix line endings (LF)", _ => options.JsonEolStyle = EolStyle.Lf },
                    { "w|windows", "use Windows line endings (CRLF)", _ => options.JsonEolStyle = EolStyle.Crlf },
                    { "minify", "remove unnecessary space but preserve comments", _ => minify = true },
                    { "z|speed-test", "write timer data instead of JSON output", _ => speedTest = true },
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

                if (outFileName == null)
                {
                    var formatter = new Formatter() { Options = options };
                    if (minify)
                        formatter.Minify(inputText, Console.Out);
                    else
                        formatter.Reformat(inputText, 0, Console.Out);
                }
                else
                {
                    var formatter = new Formatter() { Options = options };
                    using var writer = File.CreateText(outFileName);
                    if (minify)
                        formatter.Minify(inputText, writer);
                    else
                        formatter.Reformat(inputText, 0, writer);
                }

                timer.Stop();

                if (speedTest)
                    Console.WriteLine($"Processing time: {timer.Elapsed.TotalSeconds} sec");

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
