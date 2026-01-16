using System.CommandLine;
using System.Security;
using System.Text.Json;
using FracturedJson;

namespace CliNew;

public static class CliParser
{
    public static CliSettings Parse(string[] args)
    {
        var inFileArg = new Argument<FileInfo>("inputFile") { Arity = ArgumentArity.ZeroOrOne };
        inFileArg.AcceptExistingOnly();

        var configFileOpt = new Option<FileInfo>("--config")
        {
            Description = "File containing FracturedJsonOptions",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var noConfigFlagOpt = new Option<bool>("--no-config")
        {
            Description = "Do not use configuration file for FracturedJsonOptions",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = (_ => false),
        };

        var outputFileOpt = new Option<FileInfo>("--output-file")
        {
            Description = "File to write output to (overwrite)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var root = new RootCommand("Reformats a JSON document to make it highly human-readable.")
        {
            inFileArg,
            configFileOpt,
            noConfigFlagOpt,
            outputFileOpt,
        };

        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach(var errorMsg in parseResult.Errors)
                Console.Error.WriteLine(errorMsg);
            return new CliSettings() { ImmediateExitReturnCode = CliReturn.UsageError };
        }

        var inputFile = parseResult.GetValue(inFileArg);

        var fjOpts = GetFjOptionsDefaults(
            parseResult.GetRequiredValue(noConfigFlagOpt),
            parseResult.GetValue(configFileOpt),
            inputFile);

        var settings = new CliSettings()
        {
            InputFile = inputFile,
            OutputFile = parseResult.GetValue(outputFileOpt),
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
}
