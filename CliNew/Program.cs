using FracturedJson;

namespace CliNew;

internal static class Program
{
    static int Main(string[] args)
    {
        try
        {
            var settings = CliParser.Parse(args);
            if (settings.ImmediateExitReturnCode.HasValue)
                return (int)settings.ImmediateExitReturnCode.Value;

            if (settings.InputFile == null && !Console.IsInputRedirected)
            {
                Console.Error.WriteLine("No input provided. (Specify a file name or pipe input in.)");
                return (int)CliReturn.UsageError;
            }

            var elapsedTime = CliWorker.Process(settings, Console.In, Console.Out);

            if (settings.WritePerformanceInfo)
                Console.WriteLine($"FracturedJson processing time: {elapsedTime.TotalSeconds} seconds.");

            return (int)CliReturn.Success;
        }
        catch (FracturedJsonException e)
        {
            // Write only the message.  This is almost certainly a case of invalid JSON input, so a stack trace
            // is just noise.
            Console.Error.WriteLine(e.Message);
            return (int)CliReturn.InvalidJson;
        }
        catch (Exception e)
        {
            // Write the whole thing.  Who knows what's going on here.
            Console.Error.WriteLine(e);
            return (int)CliReturn.InvalidJson;
        }
    }
}
