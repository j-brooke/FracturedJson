using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using FracturedJson;
using Mono.Options;

namespace FracturedJsonCli
{
    /// <summary>
    /// Commandline app to format JSON using FracturedJson.Formatter.  Output is to standard out, or a file specified
    /// by the --outfile switch.  Input is from either standard in, or from a file if using the --file switch.
    /// Options may be set by command line switches, or by a config file.  The config file may be explicitly
    /// given with the --config switch; if not, a default config file may be used.
    /// </summary>
    public class Cli
    {
        public int Run(string[] args)
        {
            var exitCode = DetermineSettings(args);
            if (exitCode.HasValue)
                return exitCode.Value;

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

        private static readonly JsonSerializerOptions _configDeserializeOptions = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private static readonly string[] _implicitConfigFileNames = new[]
        {
            ".fracturedjson",
            ".fracturedjson.jsonc",
            ".fracturedjson.json",
        };

        private FracturedJsonOptions _fjOpts = new FracturedJsonOptions();
        private string? _inputFile;
        private string? _outputFile;
        private bool _minify;
        private bool _speedTest;

        /// <summary>
        /// Parse command line arguments.  Formatting options are taken from a config file first, if one exists,
        /// and then overridden by the commandline switches.
        /// </summary>
        private int? DetermineSettings(string[] args)
        {
            var showHelp = false;
            var noPadding = false;
            var allowComments = false;
            string? configFile = null;
            var noConfig = false;

            var primeCliOpts = new OptionSet()
            {
                { "h|help", "show this help info and exit", _ => showHelp = true },
                { "f|file=", "input from file instead of stdin", s => _inputFile = s },
                { "o|outfile=", "write output to file", s => _outputFile = s },
                { "config=", "use FracturedJsonOptions from this file", s => configFile = s },
                { "no-config", "don't load settings from a config file", _ => noConfig = true },
                { "minify", "remove unnecessary space but preserve comments", _ => _minify = true },
                { "z|speed-test", "write timer data instead of JSON output", _ => _speedTest = true },
            };

            var mainCliOpts = new OptionSet()
            {
                { "a|allow", "allow comments and trailing commas", _ => allowComments = true },
                { "c|complexity=", "maximum inline complexity", (int n) => _fjOpts.MaxInlineComplexity = n },
                { "e|expand=", "always-expand depth", (int n) => _fjOpts.AlwaysExpandDepth = n },
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
            };

            // We need to process the commandline arguments in two stages.  The first is about files and modes
            // of operation.
            var leftoverArgs = primeCliOpts.Parse(args) ?? new List<string>();

            // If they're asking for help, print it out and then exit.
            if (showHelp)
            {
                ShowHelpPrefix();
                primeCliOpts.WriteOptionDescriptions(Console.Out);
                mainCliOpts.WriteOptionDescriptions(Console.Out);
                return 0;
            }

            // If the user requested we get settings from a specific config file, use that.  Otherwise, look for
            // a default config file in the appropriate directory, or its ancestor directories.
            if (configFile != null)
            {
                var optsFromFile = ReadConfigFile(configFile);
                if (optsFromFile == null)
                {
                    Console.Error.WriteLine("Could not find config file: " + configFile);
                    return 1;
                }

                _fjOpts = optsFromFile;
            }
            else if (noConfig)
            {
                _fjOpts = new FracturedJsonOptions();
            }
            else
            {
                _fjOpts = ScanForImplicitConfigFile(_inputFile) ?? new FracturedJsonOptions();
            }

            // Now that we've got settings from config files loaded, override them with commandline switches.
            mainCliOpts.Parse(leftoverArgs);

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

            return null;
        }

        /// <summary>
        /// Reads FracturedJsonOptions from the specified file.
        /// </summary>
        private FracturedJsonOptions? ReadConfigFile(string fileName)
        {
            var file = new FileInfo(fileName);
            if (!file.Exists)
                return null;

            try
            {
                var fileContent = File.ReadAllText(file.FullName);
                var fjOpts = JsonSerializer.Deserialize<FracturedJsonOptions>(fileContent, _configDeserializeOptions);
                return fjOpts;
            }
            catch (Exception)
            {
                Console.Error.WriteLine("Error reading config file for FracturedJsonCli");
                throw;
            }
        }

        /// <summary>
        /// Looks for a config file in the starting file's directory, if any, or the current working directory.
        /// If not found there, that directory's parent is checked, etc., up to the file system root.
        /// </summary>
        private FracturedJsonOptions? ScanForImplicitConfigFile(string? startingFileLoc)
        {
            DirectoryInfo? directory;
            if (startingFileLoc != null)
            {
                var startingFile = new FileInfo(startingFileLoc);
                if (!startingFile.Exists)
                    return null;
                directory = startingFile.Directory;
            }
            else
            {
                directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            }

            while (directory != null && directory.Exists)
            {
                try
                {
                    foreach (var fileName in _implicitConfigFileNames)
                    {
                        var potentialConfigFile = new FileInfo(Path.Combine(directory.FullName, fileName));
                        if (potentialConfigFile.Exists)
                            return ReadConfigFile(potentialConfigFile.FullName)!;
                    }
                }
                catch (Exception e) when(e is UnauthorizedAccessException or SecurityException)
                {
                    // Do nothing - silently skip this directory if the user doesn't have access.  They might
                    // have access in the parent directory.
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static void ShowHelpPrefix()
        {
            var lines = new[]
            {
                $"Version {Assembly.GetExecutingAssembly().GetName().Version}",
                "Usage:",
                "  FracturedJsonCli [OPTIONS]",
                "",
                "Formats JSON producing highly readable, reasonably compact output.",
                "Input comes from STDIN unless the --file switch is used.",
                "Output goes to STDOUT unless the --outfile switch is used.",
                "",
                "Options:",
            };

            foreach (var line in lines)
                Console.Out.WriteLine(line);
        }
    }
}
