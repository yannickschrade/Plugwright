using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Plugwright.IntegrationTests;

/// <summary>
/// The application-managed pluginsettings.json: last configuration source, limited to plugin keys, written by the store.
/// </summary>
public sealed class PluginSettingsFileTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string SettingsFile => Path.Combine(_directory.Path, "pluginsettings.json");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Settings_file_wins_over_other_sources_but_only_for_plugin_keys()
    {
        File.WriteAllText(SettingsFile, """
            {
              "Plugwright": { "Plugins": { "Acme.Clock": { "Settings": { "Interval": "00:00:05" } } }, "FailureMode": "SkipAndLog" },
              "Logging": { "LogLevel": { "Default": "None" } }
            }
            """);

        using var app = Build(new()
        {
            ["Plugwright:Plugins:Acme.Clock:Settings:Interval"] = "00:01:00",
            ["Logging:LogLevel:Default"] = "Information",
        });

        var configuration = app.Services.GetRequiredService<IConfiguration>();
        configuration["Plugwright:Plugins:Acme.Clock:Settings:Interval"].ShouldBe("00:00:05");
        configuration["Logging:LogLevel:Default"].ShouldBe("Information");
        configuration["Plugwright:FailureMode"].ShouldBeNull();
    }

    [Fact]
    public void Settings_file_stays_the_last_source_when_a_plugin_adds_one()
    {
        File.WriteAllText(SettingsFile, """{ "Plugwright": { "Plugins": { "Acme.Clock": { "Settings": { "Interval": "file" } } } } }""");

        using var app = Build(configure: builder => builder.Plugins.Add<SourceAddingPlugin>());

        app.Services.GetRequiredService<IConfiguration>()["Plugwright:Plugins:Acme.Clock:Settings:Interval"].ShouldBe("file");
    }

    [Fact]
    public void Missing_file_and_missing_folder_are_fine()
    {
        using var app = Build(configure: builder => builder.Plugins.Options.SettingsFile = "state/nested/plugins.json");

        app.Services.GetRequiredService<IPluginSettingsStore>().FilePath
            .ShouldBe(Path.Combine(_directory.Path, "state", "nested", "plugins.json"));
    }

    [Fact]
    public void Turned_off_settings_file_adds_no_source_and_no_store()
    {
        File.WriteAllText(SettingsFile, """{ "Plugwright": { "Plugins": { "Acme.Clock": { "Enabled": false } } } }""");

        using var app = Build(configure: builder => builder.Plugins.Options.SettingsFile = null);

        app.Services.GetRequiredService<IConfiguration>()["Plugwright:Plugins:Acme.Clock:Enabled"].ShouldBeNull();
        app.Services.GetService<IPluginSettingsStore>().ShouldBeNull();
    }

    [Fact]
    public async Task Store_writes_settings_and_state_and_the_configuration_reloads()
    {
        File.WriteAllText(SettingsFile, """
            {
              // Written by hand once; comments are lost when the application saves.
              "Plugwright": { "plugins": { "Acme.Other": { "Enabled": false } } }
            }
            """);
        using var app = Build();
        var store = app.Services.GetRequiredService<IPluginSettingsStore>();
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var reloaded = WaitForReload(configuration, key => configuration[key] == "00:00:30", "Plugwright:Plugins:Acme.Clock:Settings:Interval");
        await store.SaveSettingsAsync("Acme.Clock", new { Interval = "00:00:30" }, cancellationToken);
        await store.SetEnabledAsync("acme.clock", false, cancellationToken);
        await reloaded.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        configuration["Plugwright:Plugins:Acme.Other:Enabled"].ShouldBe("False");
        var saved = File.ReadAllText(SettingsFile);
        saved.ShouldContain("\"Enabled\": false");
        saved.ShouldNotContain("//");
        Directory.GetFiles(_directory.Path, "*.tmp").ShouldBeEmpty();

        await WaitForReload(configuration, key => configuration[key] == "False", "Plugwright:Plugins:Acme.Clock:Enabled")
            .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }

    [Fact]
    public void Settings_file_can_be_set_in_configuration()
    {
        var builder = TestHost.CreateBuilder(_directory.Path, new Dictionary<string, string?> { ["Plugwright:SettingsFile"] = "custom.json" });
        builder.Plugins.AddFromConfiguration(builder.Configuration.GetSection("Plugwright"));

        builder.Plugins.Options.SettingsFile.ShouldBe("custom.json");
    }

    private IHost Build(Dictionary<string, string?>? settings = null, Action<HostApplicationBuilder>? configure = null)
    {
        var builder = TestHost.CreateBuilder(_directory.Path, settings);
        configure?.Invoke(builder);
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }

    /// <summary>
    /// Completes when the configuration has reloaded and the condition holds.
    /// </summary>
    private static Task WaitForReload(IConfiguration configuration, Func<string, bool> condition, string key)
    {
        var completion = new TaskCompletionSource();
        Register();
        return completion.Task;

        void Register() => configuration.GetReloadToken().RegisterChangeCallback(
            _ =>
            {
                if (condition(key))
                {
                    completion.TrySetResult();
                }
                else
                {
                    Register();
                }
            },
            state: null);
    }
}

public sealed class SourceAddingPlugin : IPlugin
{
    public void Configure(PluginContext context) =>
        context.Builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Plugwright:Plugins:Acme.Clock:Settings:Interval"] = "plugin",
        });
}
