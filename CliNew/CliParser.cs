using System.CommandLine;
using System.CommandLine.Help;
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
        if (hasHelp || hasVersion)
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

        // TODO: Override fjOpts based on CLI switches.

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

    private static readonly RootCommand _rootCommand =
        new("Reformats a JSON document to make it highly human-readable.")
        {
            _inFileArg,
            _configFileOpt,
            _noConfigFlagOpt,
            _outputFileOpt,
        };
    #endregion

}
