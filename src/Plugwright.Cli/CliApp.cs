namespace Plugwright.Cli;

/// <summary>
/// The <c>plugwright</c> command: dispatches to the subcommands.
/// </summary>
internal static class CliApp
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int UsageError = 2;

    private const string Usage = """
        Usage: plugwright <command> [options]

        Commands:
          lock <plugin-directory>    Create a lock file with the hashes of all plugin files.

        Options:
          -h, --help                 Show help.
          --version                  Show the version.

        Run 'plugwright <command> --help' for help on a command.
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            output.WriteLine(Usage);
            return args.Length == 0 ? UsageError : Success;
        }

        if (args[0] == "--version")
        {
            output.WriteLine(typeof(CliApp).Assembly.GetName().Version?.ToString(3));
            return Success;
        }

        return args[0] switch
        {
            "lock" => LockCommand.Run(args[1..], output, error),
            _ => Fail(error, $"Unknown command '{args[0]}'.\n\n{Usage}"),
        };
    }

    public static int Fail(TextWriter error, string message)
    {
        error.WriteLine(message);
        return UsageError;
    }
}
