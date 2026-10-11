using Microsoft.Extensions.Hosting;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Required plugins from directories: turned off in the settings file, or behind a dependency that is turned off.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class RequiredPluginTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Required_plugin_turned_off_in_the_settings_file_throws()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        File.WriteAllText(
            Path.Combine(_directory.Path, "pluginsettings.json"),
            $$"""{ "Plugwright": { "Plugins": { "{{GreeterId}}": { "Enabled": false } } } }""");

        var exception = Should.Throw<PluginException>(() => Apply(builder => builder.Plugins.Require(GreeterId)));

        exception.Message.ShouldContain($"Required plugin '{GreeterId}' is turned off in configuration");
    }

    [Fact]
    public void Required_plugin_behind_a_disabled_dependency_throws_before_any_plugin_runs()
    {
        testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Audit, PluginDirectory);
        testPlugins.Install(AuditReport, PluginDirectory);

        var exception = Should.Throw<PluginException>(() => Apply(
            builder => builder.Plugins.Require(AuditReportId),
            new() { [$"Plugwright:Plugins:{LoggingId}:Enabled"] = "false" }));

        exception.Message.ShouldBe(
            $"Required plugin '{AuditReportId}' cannot start because it depends on '{AuditId}', which depends on '{LoggingId}', which is disabled.");
        AppDomain.CurrentDomain.GetAssemblies().ShouldNotContain(assembly =>
            !assembly.IsDynamic && assembly.Location.StartsWith(_directory.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void Problems_of_several_required_plugins_are_reported_together()
    {
        // Audit needs Logging, which is not installed.
        testPlugins.Install(Audit, PluginDirectory);
        testPlugins.Install(Greeter, PluginDirectory);

        var exception = Should.Throw<PluginException>(() => Apply(
            builder => builder.Plugins.Require(GreeterId).Require(AuditId),
            new() { [$"Plugwright:Plugins:{GreeterId}:Enabled"] = "false" }));

        exception.Message.ShouldStartWith("Required plugins cannot start:");
        exception.Message.ShouldContain($"Required plugin '{AuditId}' ({Path.Combine(PluginDirectory, AuditId)}): It depends on plugin '{LoggingId}', which is not available.");
        exception.Message.ShouldContain($"Required plugin '{GreeterId}' is turned off");
    }

    [Fact]
    public void Required_plugins_that_fail_in_Configure_are_reported_together_at_the_end_of_phase_1()
    {
        testPlugins.Install(Logging, PluginDirectory);

        var exception = Should.Throw<PluginException>(() => Apply(
            builder =>
            {
                builder.Plugins.Add<FailingInCodePlugin>(policy => policy.IsRequired = true);
                builder.Plugins.Require(LoggingId);
            },
            new() { [$"Plugwright:Plugins:{LoggingId}:Settings:Fail"] = "true" }));

        exception.Message.ShouldStartWith("Required plugins cannot start:");
        exception.Message.ShouldContain("Required plugin 'Plugwright.IntegrationTests' (added in code): Phase 1 failed");
        exception.Message.ShouldContain($"Required plugin '{LoggingId}'");
        exception.Message.ShouldContain("Logging failed on purpose.");
    }

    private void Apply(Action<HostApplicationBuilder> configure, Dictionary<string, string?>? settings = null)
    {
        var builder = TestHost.CreateBuilder(_directory.Path, settings);
        builder.Plugins.AddFromDirectory(PluginDirectory);
        configure(builder);
        builder.ApplyPlugins();
    }
}

public sealed class FailingInCodePlugin : IPlugin
{
    public void Configure(PluginContext context) => throw new InvalidOperationException("Failed in code on purpose.");
}
