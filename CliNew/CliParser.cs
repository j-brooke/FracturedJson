using System.CommandLine;

namespace CliNew;

public static class CliParser
{
    public static CliSettings Parse(string[] args)
    {
        var inFileArg = new Argument<FileInfo>("inputFile") { Arity = ArgumentArity.ZeroOrOne };
        inFileArg.AcceptExistingOnly();

        var root = new RootCommand("Reformats a JSON document to make it highly human-readable.")
        {
            inFileArg,
        };

        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach(var errorMsg in parseResult.Errors)
                Console.Error.WriteLine(errorMsg);
            return new CliSettings() { ImmediateExitReturnCode = CliReturn.UsageError };
        }

        var settings = new CliSettings()
        {
            InputFile = parseResult.GetValue(inFileArg),
        };

        return settings;
    }
}
