namespace Plugwright.Cli;

/// <summary>
/// <c>plugwright lock &lt;plugin-directory&gt; [--output &lt;file&gt;]</c>: writes the lock file for the HashVerifier.
/// </summary>
internal static class LockCommand
{
    private const string DefaultOutput = "plugins.lock.json";

    private const string Usage = $"""
        Usage: plugwright lock <plugin-directory>... [--output <file>]

        Creates a lock file with the sha256 hash of every file in every plugin folder of <plugin-directory>,
        and of every NuGet package (.nupkg) in it. The HashVerifier loads only plugins and packages whose files
        match the lock file exactly.

        Arguments:
          <plugin-directory>       The plugin directory with one subfolder per plugin, or a folder of packages.
                                   Several directories go into one lock file, for example a package folder
                                   and its download folder.

        Options:
          -o, --output <file>      The lock file to write. Default: {DefaultOutput}
          -h, --help               Show help.

        Store the lock file where whoever can write to the plugin directory cannot change it.
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        List<string> pluginDirectories = [];
        var lockFile = DefaultOutput;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    output.WriteLine(Usage);
                    return CliApp.Success;

                case "-o" or "--output" when i + 1 < args.Length:
                    lockFile = args[++i];
                    break;

                case "-o" or "--output":
                    return CliApp.Fail(error, $"Option '{args[i]}' needs a file name.\n\n{Usage}");

                case var option when option.StartsWith('-'):
                    return CliApp.Fail(error, $"Unknown option '{option}'.\n\n{Usage}");

                default:
                    pluginDirectories.Add(args[i]);
                    break;
            }
        }

        if (pluginDirectories.Count == 0)
        {
            return CliApp.Fail(error, $"The plugin directory is missing.\n\n{Usage}");
        }

        foreach (var pluginDirectory in pluginDirectories)
        {
            if (!Directory.Exists(pluginDirectory))
            {
                error.WriteLine($"The plugin directory '{pluginDirectory}' does not exist.");
                return CliApp.Failure;
            }

            foreach (var file in Directory.EnumerateFiles(pluginDirectory).Where(file => !file.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
            {
                error.WriteLine($"Warning: '{file}' is neither in a plugin folder nor a package and is not part of the lock file.");
            }
        }

        PluginLockFile lockFileContent;
        try
        {
            lockFileContent = PluginLockFile.Create(pluginDirectories);
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(exception.Message);
            return CliApp.Failure;
        }

        lockFileContent.Write(lockFile);


        var fileCount = lockFileContent.Plugins.Values.Sum(files => files.Count);
        output.WriteLine($"Wrote '{lockFile}' with {Count(lockFileContent.Plugins.Count, "plugin")} and {Count(fileCount, "file")}.");
        return CliApp.Success;
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
