using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using FracturedJson;
using Mono.Options;

namespace FracturedJsonCli
{
    /// <summary>
    /// Commandline app to format JSON using FracturedJson.Formatter.  Output is to standard out, or a file specified
    /// by the --outfile switch.  Input is from either standard in, or from a file if using the --file switch.
    /// </summary>
    public class Cli
    {
        public int Run(string[] args)
        {
            DetermineSettings(args);

            string inputText;
            if (_inputFile != null)
            {
                inputText = File.ReadAllText(_inputFile);
            }
            else if (Console.IsInputRedirected)
            {
                inputText = Console.In.ReadToEnd();
            }
            else
            {
                Console.Error.WriteLine("Please provide an input file with -f or --file, or pipe some in.");
                return 1;
            }

            var timer = Stopwatch.StartNew();

            if (_outputFile == null)
            {
                var formatter = new Formatter() { Options = _fjOpts };
                if (_minify)
                    formatter.Minify(inputText, Console.Out);
                else
                    formatter.Reformat(inputText, 0, Console.Out);
            }
            else
            {
                var formatter = new Formatter() { Options = _fjOpts };
                using var writer = File.CreateText(_outputFile);
                if (_minify)
                    formatter.Minify(inputText, writer);
                else
                    formatter.Reformat(inputText, 0, writer);
            }

            timer.Stop();

            if (_speedTest)
                Console.WriteLine($"Processing time: {timer.Elapsed.TotalSeconds} sec");

            return 0;
        }

        public static int Main(string[] args)
        {
            try
            {
                var app = new Cli();
                var rtnVal = app.Run(args);
                return rtnVal;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private FracturedJsonOptions _fjOpts = new FracturedJsonOptions();
        private string? _inputFile;
        private string? _outputFile;
        private bool _minify;
        private bool _speedTest;

        private bool DetermineSettings(string[] args)
        {
            var showHelp = false;
            var noPadding = false;
            var allowComments = false;

            var cliOpts = new OptionSet()
            {
                { "a|allow", "allow comments and trailing commas", _ => allowComments = true },
                { "c|complexity=", "maximum inline complexity", (int n) => _fjOpts.MaxInlineComplexity = n },
                { "e|expand=", "always-expand depth", (int n) => _fjOpts.AlwaysExpandDepth = n },
                { "f|file=", "input from file instead of stdin", s => _inputFile = s },
                { "h|help", "show this help info and exit", _ => showHelp = true },
                {
                    "j|justify=", "number list justification [l,r,d,n]", s =>
                        _fjOpts.NumberListAlignment = s.ToUpper() switch
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
                        _fjOpts.TableCommaPlacement = s.ToUpper() switch
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
                    (int n) => _fjOpts.MaxTotalLineLength = n
                },
                {
                    "m|multiline=",
                    "maximum multi-line array complexity",
                    (int n) => _fjOpts.MaxCompactArrayComplexity = n
                },
                { "n|name-padding=", "max property name padding", (int n) => _fjOpts.MaxPropNamePadding = n},
                { "o|outfile=", "write output to file", s => _outputFile = s },
                { "p|no-padding", "don't include padding spaces", _ => noPadding = true },
                {
                    "s|space=",
                    "use this many spaces per indent level",
                    (int n) => _fjOpts.IndentSpaces = n
                },
                { "r|row=", "maximum table row complexity", (int n) => _fjOpts.MaxTableRowComplexity = n },
                { "t|tab", "use tabs for indentation", _ => _fjOpts.UseTabToIndent = true },
                { "u|unix", "use Unix line endings (LF)", _ => _fjOpts.JsonEolStyle = EolStyle.Lf },
                { "w|windows", "use Windows line endings (CRLF)", _ => _fjOpts.JsonEolStyle = EolStyle.Crlf },
                { "minify", "remove unnecessary space but preserve comments", _ => _minify = true },
                { "z|speed-test", "write timer data instead of JSON output", _ => _speedTest = true },
            };

            cliOpts.Parse(args);

            if (showHelp)
            {
                ShowHelpPrefix();
                cliOpts.WriteOptionDescriptions(Console.Out);
                return false;
            }

            if (noPadding)
            {
                _fjOpts.ColonPadding = false;
                _fjOpts.CommaPadding = false;
                _fjOpts.CommentPadding = false;
                _fjOpts.NestedBracketPadding = false;
            }

            if (allowComments)
            {
                _fjOpts.CommentPolicy = CommentPolicy.Preserve;
                _fjOpts.AllowTrailingCommas = true;
                _fjOpts.PreserveBlankLines = true;
            }

            return true;
        }

        private static void ShowHelpPrefix()
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
        }
    }
}
