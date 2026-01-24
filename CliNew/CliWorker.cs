using System.Diagnostics;
using FracturedJson;
using Wcwidth;

namespace CliNew;

/// <summary>
/// Class responsible for actually reading input, reformatting JSON, and writing it to a file or stdout.
/// </summary>
public static class CliWorker
{
    public static TimeSpan Process(CliSettings settings, TextReader stdIn, TextWriter stdOut)
    {
        StreamWriter? fileWriter = null;
        try
        {
            string inputJson;
            if (settings.InputFile != null)
            {
                using var inFileReader = new StreamReader(settings.InputFile.FullName, true);
                inputJson = inFileReader.ReadToEnd();
            }
            else
            {
                inputJson = stdIn.ReadToEnd();
            }

            TextWriter outWriter;
            if (settings.OutputFile != null)
            {
                fileWriter = new StreamWriter(settings.OutputFile.FullName);
                outWriter = fileWriter;
            }
            else
            {
                outWriter = stdOut;
            }

            var timer = Stopwatch.StartNew();

            var formatter = new Formatter() { Options = settings.FjOptions };

            if (settings.EastAsianWideChars)
                formatter.StringLengthFunc = FullUnicodeWidth;

            if (settings.Minify)
                formatter.Minify(inputJson, outWriter);
            else
                formatter.Reformat(inputJson, 0, outWriter);

            return timer.Elapsed;
        }
        finally
        {
            fileWriter?.Dispose();
        }
    }

    private static int FullUnicodeWidth(string str)
    {
        return UnicodeCalculator.GetWidth(str);
    }
}
