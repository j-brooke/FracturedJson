namespace Cli;

/// <summary>
/// Exit codes from the CLI process.
/// </summary>
public enum CliReturn
{
    Success = 0,

    /// <summary>
    /// Malformed JSON input, or a file error.
    /// </summary>
    InvalidJson = 1,

    /// <summary>
    /// A problem with commandline processing, such as unknown options or values.
    /// </summary>
    UsageError = 2,
}
