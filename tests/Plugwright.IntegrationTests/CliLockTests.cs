using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Plugwright.Cli;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

[Collection(LoadContextTests.Name)]
public sealed class CliLockTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    private string LockFile => Path.Combine(_directory.Path, "plugins.lock.json");

    public void Dispose()
    {
        _directory.Dispose();
        _output.Dispose();
        _error.Dispose();
    }

    [Fact]
    public void Lock_file_from_the_cli_passes_the_HashVerifier()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        testPlugins.Install(Counter, PluginDirectory);

        Run("lock", PluginDirectory, "--output", LockFile).ShouldBe(CliApp.Success);

        _output.ToString().ShouldContain("with 2 plugins and");
        var builder = TestHost.CreateBuilder(_directory.Path);
        builder.PluginVerifiers.AddHashVerifier();
        builder.Plugins.AddFromDirectory(PluginDirectory);
        builder.ApplyPlugins();
        var report = builder.Build().Services.GetRequiredService<PluginReport>();
        report.Verifications.Count.ShouldBe(2);
        report.Verifications.ShouldAllBe(verification => verification.Succeeded);
    }

    [Fact]
    public void Lock_file_lists_every_file_with_a_sha256_hash()
    {
        var folder = testPlugins.Install(Greeter, PluginDirectory);

        Run("lock", PluginDirectory, "-o", LockFile).ShouldBe(CliApp.Success);

        using var document = JsonDocument.Parse(File.ReadAllText(LockFile));
        document.RootElement.GetProperty("version").GetInt32().ShouldBe(1);
        var files = document.RootElement.GetProperty("plugins").GetProperty(GreeterId).GetProperty("files").EnumerateObject().ToList();
        files.Select(file => file.Name).ShouldBe(Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        files.ShouldAllBe(file => file.Value.GetString()!.StartsWith("sha256-", StringComparison.Ordinal));
        File.ReadAllText(LockFile).ShouldNotContain("\\u");
    }

    [Fact]
    public void Writes_plugins_lock_json_in_the_current_directory_by_default()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        var previous = Environment.CurrentDirectory;
        Environment.CurrentDirectory = _directory.Path;
        try
        {
            Run("lock", "plugins").ShouldBe(CliApp.Success);
        }
        finally
        {
            Environment.CurrentDirectory = previous;
        }

        File.Exists(LockFile).ShouldBeTrue();
    }

    [Fact]
    public void Files_outside_plugin_folders_are_reported()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        File.WriteAllText(Path.Combine(PluginDirectory, "Loose.dll"), "loose");

        Run("lock", PluginDirectory, "-o", LockFile).ShouldBe(CliApp.Success);

        _error.ToString().ShouldContain("Loose.dll' is not in a plugin folder");
    }

    [Fact]
    public void Missing_plugin_directory_fails()
    {
        Run("lock", PluginDirectory, "-o", LockFile).ShouldBe(CliApp.Failure);

        _error.ToString().ShouldContain("does not exist");
        File.Exists(LockFile).ShouldBeFalse();
    }

    [Theory]
    [InlineData]
    [InlineData("unknown")]
    [InlineData("lock")]
    [InlineData("lock", "a", "b")]
    [InlineData("lock", "plugins", "--output")]
    [InlineData("lock", "plugins", "--force")]
    public void Invalid_usage_fails_with_exit_code_2(params string[] args)
    {
        Run(args).ShouldBe(CliApp.UsageError);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("lock", "--help")]
    public void Help_succeeds(params string[] args)
    {
        Run(args).ShouldBe(CliApp.Success);

        _output.ToString().ShouldContain("Usage: plugwright");
    }

    private int Run(params string[] args) => CliApp.Run(args, _output, _error);
}
