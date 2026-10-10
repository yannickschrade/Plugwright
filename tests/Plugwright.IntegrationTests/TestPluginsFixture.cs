using System.Reflection;

[assembly: AssemblyFixture(typeof(Plugwright.IntegrationTests.TestPluginsFixture))]

namespace Plugwright.IntegrationTests;

/// <summary>
/// Knows where the built test plugins are and installs them into plugin directories.
/// The test plugins are built together with this project (see the project file).
/// </summary>
public sealed class TestPluginsFixture
{
    public const string Greeter = "Greeter";
    public const string Counter = "Counter";
    public const string NoPlugins = "NoPlugins";
    public const string Logging = "Logging";
    public const string Audit = "Audit";
    public const string NeedsNewLogging = "NeedsNewLogging";
    public const string CycleA = "CycleA";
    public const string CycleB = "CycleB";
    public const string Web = "Web";
    public const string AuditReport = "AuditReport";
    public const string Faulty = "Faulty";

    public const string GreeterId = "Plugwright.TestPlugins.Greeter";
    public const string CounterId = "Plugwright.TestPlugins.Counter";
    public const string NoPluginsId = "Plugwright.TestPlugins.NoPlugins";
    public const string LoggingId = "Plugwright.TestPlugins.Logging";
    public const string AuditId = "Plugwright.TestPlugins.Audit";
    public const string WebId = "Plugwright.TestPlugins.Web";
    public const string AuditReportId = "Plugwright.TestPlugins.AuditReport";
    public const string FaultyId = "Plugwright.TestPlugins.Faulty";

    private static readonly Dictionary<string, string> s_pluginIds = new()
    {
        [Greeter] = GreeterId,
        [Counter] = CounterId,
        [NoPlugins] = NoPluginsId,
        [Logging] = LoggingId,
        [Audit] = AuditId,
        [NeedsNewLogging] = "Plugwright.TestPlugins.NeedsNewLogging",
        [CycleA] = "Plugwright.TestPlugins.CycleA",
        [CycleB] = "Plugwright.TestPlugins.CycleB",
        [Web] = WebId,
        [AuditReport] = AuditReportId,
        [Faulty] = FaultyId,
    };

    public TestPluginsFixture()
    {
        BuildOutputPath = typeof(TestPluginsFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "TestPluginsPath")
            .Value!;

        foreach (var project in s_pluginIds.Keys)
        {
            if (!Directory.Exists(Path.Combine(BuildOutputPath, project)))
            {
                throw new InvalidOperationException(
                    $"Test plugin '{project}' was not built to '{BuildOutputPath}'. Build the solution before running the tests.");
            }
        }
    }

    public string BuildOutputPath { get; }

    /// <summary>
    /// Gets the folder with the test packages (.nupkg) built from tests/TestPackages.
    /// </summary>
    public string PackagesPath { get; } = typeof(TestPluginsFixture).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "TestPackagesPath")
        .Value!;

    /// <summary>
    /// Copies a test package, for example <c>Plugwright.TestPackages.Library.1.0.0</c>, into a package folder.
    /// </summary>
    /// <returns>The path of the copied package.</returns>
    public string InstallPackage(string package, string packageDirectory)
    {
        Directory.CreateDirectory(packageDirectory);
        var target = Path.Combine(packageDirectory, $"{package}.nupkg");
        File.Copy(Path.Combine(PackagesPath, $"{package}.nupkg"), target);
        return target;
    }

    /// <summary>
    /// Copies the build output of a test plugin project into its own folder in the plugin directory,
    /// as <c>{pluginDirectory}/{PluginId}/</c>.
    /// </summary>
    /// <returns>The plugin folder.</returns>
    public string Install(string project, string pluginDirectory)
    {
        var folder = Path.Combine(pluginDirectory, s_pluginIds[project]);
        CopyDirectory(Path.Combine(BuildOutputPath, project), folder);
        return folder;
    }

    /// <summary>
    /// Returns the path of a test plugin's main assembly in the build output.
    /// </summary>
    public string MainAssemblyPath(string project) =>
        Path.Combine(BuildOutputPath, project, $"{s_pluginIds[project]}.dll");

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var targetFile = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile);
        }
    }
}
