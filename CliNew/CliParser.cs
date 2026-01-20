using System.CommandLine;
using System.CommandLine.Help;
using System.Reflection;
using System.Security;
using System.Text.Json;
using FracturedJson;

namespace CliNew;

public static class CliParser
{
    public static CliSettings Parse(string[] args)
    {
        var parseResult = _rootCommand.Parse(args);

        // Write errors to StdErr if there are any, and then exit.
        if (parseResult.Errors.Count > 0)
        {
            foreach(var errorMsg in parseResult.Errors)
                Console.Error.WriteLine(errorMsg);
            return new CliSettings() { ImmediateExitReturnCode = CliReturn.UsageError };
        }

        // If a help or version switch exists, let System.CommandLine's default behavior handle them.
        var helpOpt = (HelpOption?)_rootCommand.Options.FirstOrDefault(opt => opt is HelpOption);
        var versionOpt = (VersionOption?)_rootCommand.Options.FirstOrDefault(opt => opt is VersionOption);

        var hasHelp = helpOpt != null && parseResult.GetResult(helpOpt) != null;
        var hasVersion = versionOpt != null && parseResult.GetResult(versionOpt) != null;
        if (hasHelp)
        {
            parseResult.Invoke();

            var libVersion = Assembly.GetAssembly(typeof(Formatter))?.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"FracturedJson library version: {libVersion}");

            return new CliSettings() { ImmediateExitReturnCode = CliReturn.Success };
        }
        else if (hasVersion)
        {
            parseResult.Invoke();
            return new CliSettings() { ImmediateExitReturnCode = CliReturn.Success };
        }

        // Figure out the starting FracturedJsonOptions from config files, if any.
        var inputFile = parseResult.GetValue(_inFileArg);
        var fjOpts = GetFjOptionsDefaults(
            parseResult.GetRequiredValue(_noConfigFlagOpt),
            parseResult.GetValue(_configFileOpt),
            inputFile);

        ApplyFjOptsFromCommandline(fjOpts, parseResult);

        var settings = new CliSettings()
        {
            InputFile = inputFile,
            OutputFile = parseResult.GetValue(_outputFileOpt),
            FjOptions = fjOpts,
        };

        return settings;
    }

    private static readonly string[] _implicitConfigFileNames =
    [
        ".fracturedjson",
        ".fracturedjson.jsonc",
        ".fracturedjson.json"
    ];


    private static FracturedJsonOptions GetFjOptionsDefaults(bool noConfigFlag, FileInfo? explicitConfigFile,
        FileInfo? inputFile)
    {
        if (noConfigFlag)
            return new FracturedJsonOptions();

        if (explicitConfigFile != null)
            return ReadConfigFile(explicitConfigFile) ??  new FracturedJsonOptions();

        return ScanForImplicitConfigFile(inputFile) ?? new FracturedJsonOptions();
    }

    /// <summary>
    /// Reads FracturedJsonOptions from the specified file.
    /// </summary>
    private static FracturedJsonOptions? ReadConfigFile(FileInfo file)
    {
        // Use AoT JSON deserialization info instead of reflection to appease make things easier for
        // MSBuild's trim option.
        var fileContent = File.ReadAllText(file.FullName);
        var fjOpts = JsonSerializer.Deserialize(fileContent,
            FjOptsSerializationContext.Default.FracturedJsonOptions);
        return fjOpts;
    }

    /// <summary>
    /// Looks for a config file in the starting file's directory, if any, or the current working directory.
    /// If not found there, that directory's parent is checked, etc., up to the file system root.
    /// </summary>
    private static FracturedJsonOptions? ScanForImplicitConfigFile(FileInfo? startingFileLoc)
    {
        var directory = (startingFileLoc != null)
            ? startingFileLoc.Directory
            : new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory != null && directory.Exists)
        {
            try
            {
                foreach (var fileName in _implicitConfigFileNames)
                {
                    var potentialConfigFile = new FileInfo(Path.Combine(directory.FullName, fileName));
                    if (potentialConfigFile.Exists)
                        return ReadConfigFile(potentialConfigFile)!;
                }
            }
            catch (Exception e) when(e is UnauthorizedAccessException or SecurityException)
            {
                // Do nothing - silently skip this directory if the user doesn't have access.  They might
                // still have access in the parent directory, though.
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void ApplyFjOptsFromCommandline(FracturedJsonOptions fjOpts, ParseResult parseResult)
    {
        fjOpts.JsonEolStyle = parseResult.GetValue(_jsonEolStyleOpt) ?? fjOpts.JsonEolStyle;
        fjOpts.MaxTotalLineLength = parseResult.GetValue(_maxTotalLineLengthOpt) ?? fjOpts.MaxTotalLineLength;
        fjOpts.MaxInlineComplexity = parseResult.GetValue(_maxInlineComplexityOpt) ?? fjOpts.MaxInlineComplexity;
        fjOpts.MaxCompactArrayComplexity =
            parseResult.GetValue(_maxCompactArrayComplexityOpt) ?? fjOpts.MaxCompactArrayComplexity;
        fjOpts.MaxTableRowComplexity = parseResult.GetValue(_maxTableRowComplexityOpt) ?? fjOpts.MaxTableRowComplexity;
        fjOpts.MaxPropNamePadding = parseResult.GetValue(_maxPropNamePaddingOpt) ?? fjOpts.MaxPropNamePadding;
        fjOpts.ColonBeforePropNamePadding =
            parseResult.GetValue(_colonBeforePropNamePaddingOpt) ?? fjOpts.ColonBeforePropNamePadding;
        fjOpts.TableCommaPlacement = (TableCommaPlacement?)parseResult.GetValue(_tableCommaPlacementOpt) ??
                                     fjOpts.TableCommaPlacement;
        fjOpts.MinCompactArrayRowItems =
            parseResult.GetValue(_minCompactArrayRowItemsOpt) ?? fjOpts.MinCompactArrayRowItems;
        fjOpts.AlwaysExpandDepth = parseResult.GetValue(_alwaysExpandDepthOpt) ?? fjOpts.AlwaysExpandDepth;
        fjOpts.NestedBracketPadding = parseResult.GetValue(_nestedBracketPaddingOpt) ?? fjOpts.NestedBracketPadding;
        fjOpts.SimpleBracketPadding = parseResult.GetValue(_simpleBracketPaddingOpt) ?? fjOpts.SimpleBracketPadding;
        fjOpts.ColonPadding = parseResult.GetValue(_colonPaddingOpt) ?? fjOpts.ColonPadding;
        fjOpts.CommaPadding = parseResult.GetValue(_commaPaddingOpt) ?? fjOpts.CommaPadding;
        fjOpts.CommentPadding = parseResult.GetValue(_commentPaddingOpt) ?? fjOpts.CommentPadding;
        fjOpts.NumberListAlignment = (NumberListAlignment?)parseResult.GetValue(_numberListAlignmentOpt) ??
                                     fjOpts.NumberListAlignment;
        fjOpts.IndentSpaces = parseResult.GetValue(_indentSpacesOpt) ?? fjOpts.IndentSpaces;
        fjOpts.UseTabToIndent = parseResult.GetValue(_useTabToIndentOpt) ?? fjOpts.UseTabToIndent;
        fjOpts.PrefixString = parseResult.GetValue(_prefixStringOpt) ?? fjOpts.PrefixString;
        fjOpts.CommentPolicy = parseResult.GetValue(_commentPolicyOpt) ?? fjOpts.CommentPolicy;
        fjOpts.PreserveBlankLines = parseResult.GetValue(_preserveBlankLinesOpt) ?? fjOpts.PreserveBlankLines;
        fjOpts.AllowTrailingCommas = parseResult.GetValue(_allowTrailingCommasOpt) ?? fjOpts.AllowTrailingCommas;
    }

    // Option Declarations region: defines all commandline switches and their behavior.
    #region OptionsDeclarations
    private static readonly Argument<FileInfo> _inFileArg = MakeInFileArg();
    private static Argument<FileInfo> MakeInFileArg()
    {
        return new Argument<FileInfo>("inputFile") { Arity = ArgumentArity.ZeroOrOne }
            .AcceptExistingOnly();
    }

    private static readonly Option<FileInfo> _configFileOpt = MakeConfigFileOpt();
    private static Option<FileInfo> MakeConfigFileOpt()
    {
        return new Option<FileInfo>("--config")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = "File containing FracturedJsonOptions",
            }
            .AcceptExistingOnly();
    }

    private static readonly Option<bool> _noConfigFlagOpt = new("--no-config")
    {
        Description = "Do not use configuration file for FracturedJsonOptions",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (_ => false),
    };

    private static readonly Option<FileInfo> _outputFileOpt = new("--output-file")
    {
        Description = "File to write output to (overwrite)",
        Arity = ArgumentArity.ZeroOrOne,
    };

    private static readonly Option<EolStyle?> _jsonEolStyleOpt = new("--eol", "--JsonEolStyle")
        { Description = "Character sequence for newlines", };

    private static readonly Option<int?> _maxTotalLineLengthOpt = new("--length", "--MaxTotalLineLength", "-l")
        { Description = "Maximum characters per line including indentation", };

    private static readonly Option<int?> _maxInlineComplexityOpt = new("--inline-complexity", "--MaxInlineComplexity")
        { Description = "Max nesting level for inline objects/arrays", };

    private static readonly Option<int?> _maxCompactArrayComplexityOpt = new("--array-complexity", "--MaxCompactArrayComplexity")
        { Description = "Max nesting level for arrays with multiple items per row", };

    private static readonly Option<int?> _maxTableRowComplexityOpt = new("--table-complexity", "--MaxTableRowComplexity")
        { Description = "Max nesting level for table formatting", };

    private static readonly Option<int?> _maxPropNamePaddingOpt = new("--prop-padding", "--MaxPropNamePadding")
        { Description = "Max number of spaces to line up property values", };

    private static readonly Option<bool?> _colonBeforePropNamePaddingOpt = new("--colon-before-padding", "--ColonBeforePropNamePadding")
        { Description = "Put colons next to property names instead of after padding", };

    private static readonly Option<TableCommaPlacementCli?> _tableCommaPlacementOpt = new(
        "--table-comma", "--TableCommaPlacement")
        { Description = "Where to put commas in tables relative to padding", };

    private static readonly Option<int?> _minCompactArrayRowItemsOpt = new("--min-array-items", "--MinCompactArrayRowItems")
        { Description = "Minimum items per row for compact arrays", };

    private static readonly Option<int?> _alwaysExpandDepthOpt = new("--always-expand", "--AlwaysExpandDepth")
        { Description = "Depth from root at which objects/arrays are always expanded", };

    private static readonly Option<bool?> _nestedBracketPaddingOpt = new("--nested-padding", "--NestedBracketPadding")
        { Description = "Spaces inside brackets for complex objects/arrays", };

    private static readonly Option<bool?> _simpleBracketPaddingOpt = new("--simple-padding", "--SimpleBracketPadding")
        { Description = "Spaces inside brackets for simple objects/arrays", };

    private static readonly Option<bool?> _colonPaddingOpt = new("--colon-padding", "--ColonPadding")
        { Description = "Space after colon", };

    private static readonly Option<bool?> _commaPaddingOpt = new("--comma-padding", "--CommaPadding")
        { Description = "Space after comma", };

    private static readonly Option<bool?> _commentPaddingOpt = new("--comment-padding", "--CommentPadding")
        { Description = "Space between comment and nearby values", };

    private static readonly Option<NumberListAlignmentCli?> _numberListAlignmentOpt = new("--number-alignment", "--NumberListAlignment")
        { Description = "How sequences of numbers are aligned", };

    private static readonly Option<int?> _indentSpacesOpt = new("--spaces", "--IndentSpaces")
        { Description = "Number of spaces per indent level", };

    private static readonly Option<bool?> _useTabToIndentOpt = new("--use-tabs", "--UseTabToIndent")
        { Description = "Use a tab character to indent instead of spaces", };

    private static readonly Option<string?> _prefixStringOpt = new("--prefix", "--PrefixString")
        { Description = "String to put at the start of each line", };

    private static readonly Option<CommentPolicy?> _commentPolicyOpt = new("--comment-policy", "--CommentPolicy")
        { Description = "How comments should be handled", };

    private static readonly Option<bool?> _preserveBlankLinesOpt = new("--preserve-blanks", "--PreserveBlankLines")
        { Description = "Blank lines in input should be included in output", };

    private static readonly Option<bool?> _allowTrailingCommasOpt = new("--trailing-commas", "--AllowTrailingCommas")
        { Description = "Allow a comma after the last item in an array/object", };

    private static readonly RootCommand _rootCommand =
        new("Reformats a JSON document to make it highly human-readable.")
        {
            _inFileArg,
            _configFileOpt,
            _noConfigFlagOpt,
            _outputFileOpt,

            _jsonEolStyleOpt,
            _maxTotalLineLengthOpt,
            _maxInlineComplexityOpt,
            _maxCompactArrayComplexityOpt,
            _maxTableRowComplexityOpt,
            _maxPropNamePaddingOpt,
            _colonBeforePropNamePaddingOpt,
            _tableCommaPlacementOpt,
            _minCompactArrayRowItemsOpt,
            _alwaysExpandDepthOpt,
            _nestedBracketPaddingOpt,
            _simpleBracketPaddingOpt,
            _colonPaddingOpt,
            _commaPaddingOpt,
            _commentPaddingOpt,
            _numberListAlignmentOpt,
            _indentSpacesOpt,
            _useTabToIndentOpt,
            _prefixStringOpt,
            _commentPolicyOpt,
            _preserveBlankLinesOpt,
            _allowTrailingCommasOpt,
        };
    #endregion

}
