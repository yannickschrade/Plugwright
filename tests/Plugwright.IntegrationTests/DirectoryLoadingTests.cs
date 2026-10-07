using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

[Collection(LoadContextTests.Name)]
public sealed class DirectoryLoadingTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Loads_all_plugins_from_their_folders()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        testPlugins.Install(Counter, PluginDirectory);

        var app = BuildApp(builder => builder.Plugins.AddFromDirectory(PluginDirectory));

        var configuration = app.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        configuration["TestPlugins:Greeter:Message"].ShouldBe("Hello");
        configuration["TestPlugins:Greeter:Id"].ShouldBe(GreeterId);
        configuration["TestPlugins:Farewell:Id"].ShouldBe(GreeterId);
        app.Services.GetServices<IHostedService>().ShouldContain(service => service.GetType().Name == "CounterService");

        var loaded = LogRecords(app).Where(record => record.Id.Id == 1000).Select(record => record.Message).ToList();
        loaded.Count.ShouldBe(3);
        loaded.ShouldContain(message => message.Contains("Plugwright.TestPlugins.Greeter.GreeterPlugin") && message.Contains("1.2.0"));
        loaded.ShouldContain(message => message.Contains("Plugwright.TestPlugins.Greeter.FarewellPlugin"));
        loaded.ShouldContain(message => message.Contains("Plugwright.TestPlugins.Counter.CounterPlugin"));
        loaded.ShouldAllBe(message => message.Contains(PluginDirectory));
    }

    [Fact]
    public void Plugin_reads_its_settings_by_plugin_id()
    {
        testPlugins.Install(Greeter, PluginDirectory);

        var app = BuildApp(
            builder => builder.Plugins.AddFromDirectory(PluginDirectory),
            new Dictionary<string, string?> { [$"Plugwright:Plugins:{GreeterId}:Settings:Message"] = "Hi" });

        app.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["TestPlugins:Greeter:Message"]
            .ShouldBe("Hi");
    }

    [Fact]
    public void Relative_path_is_resolved_against_the_content_root()
    {
        testPlugins.Install(Greeter, PluginDirectory);

        var app = BuildApp(builder => builder.Plugins.AddFromDirectory("plugins"));

        LogRecords(app).Where(record => record.Id.Id == 1000).Count().ShouldBe(2);
    }

    [Fact]
    public void Each_plugin_gets_its_own_load_context_and_private_dependencies()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        testPlugins.Install(Counter, PluginDirectory);

        var app = BuildApp(builder => builder.Plugins.AddFromDirectory(PluginDirectory));

        // Greeter ships TextLib 1.0, Counter ships TextLib 2.0 with the same assembly name.
        var configuration = app.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        configuration["TestPlugins:Greeter:TextLibVersion"].ShouldBe("1");
        configuration["TestPlugins:Farewell:TextLibVersion"].ShouldBe("1");
        configuration["TestPlugins:Counter:TextLibVersion"].ShouldBe("2");

        var contexts = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && assembly.Location.StartsWith(PluginDirectory, StringComparison.Ordinal))
            .GroupBy(assembly => AssemblyLoadContext.GetLoadContext(assembly)!)
            .ToDictionary(group => group.Key.Name!, group => group.Select(assembly => assembly.GetName().Name).Order().ToList());

        contexts.Keys.Order().ShouldBe([CounterId, GreeterId]);
        contexts[GreeterId].ShouldBe([GreeterId, "Plugwright.TestPlugins.TextLib"]);
        contexts[CounterId].ShouldBe([CounterId, "Plugwright.TestPlugins.TextLib"]);
    }

    [Fact]
    public void Shared_assemblies_come_from_the_host_even_when_the_plugin_ships_them()
    {
        // Counter copies Plugwright.Abstractions and Microsoft.Extensions.* into its folder.
        File.Exists(Path.Combine(testPlugins.Install(Counter, PluginDirectory), "Plugwright.Abstractions.dll")).ShouldBeTrue();

        var app = BuildApp(builder => builder.Plugins.AddFromDirectory(PluginDirectory));

        var counterService = app.Services.GetServices<IHostedService>().Single(service => service.GetType().Name == "CounterService");
        var pluginContext = AssemblyLoadContext.GetLoadContext(counterService.GetType().Assembly)!;
        pluginContext.ShouldNotBe(AssemblyLoadContext.Default);
        pluginContext.Name.ShouldBe(CounterId);

        // The plugin's IPlugin and IHostedService are the host's types.
        var pluginType = counterService.GetType().Assembly.GetType("Plugwright.TestPlugins.Counter.CounterPlugin")!;
        typeof(IPlugin).IsAssignableFrom(pluginType).ShouldBeTrue();
        pluginContext.Assemblies.ShouldNotContain(assembly => assembly.GetName().Name == "Plugwright.Abstractions");
        pluginContext.Assemblies.ShouldNotContain(assembly => assembly.GetName().Name!.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_directory_logs_a_warning_and_loads_nothing()
    {
        var app = BuildApp(builder => builder.Plugins.AddFromDirectory(PluginDirectory));

        var warning = LogRecords(app).Where(record => record.Id.Id == 1002).ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(PluginDirectory);
        LogRecords(app).ShouldNotContain(record => record.Id.Id == 1000);
    }

    [Fact]
    public void Assembly_outside_a_plugin_folder_is_ignored_with_a_warning()
    {
        Directory.CreateDirectory(PluginDirectory);
        var looseAssembly = Path.Combine(PluginDirectory, $"{GreeterId}.dll");
        File.Copy(testPlugins.MainAssemblyPath(Greeter), looseAssembly);

        var app = BuildApp(builder => builder.Plugins.AddFromDirectory(PluginDirectory));

        var warning = LogRecords(app).Where(record => record.Id.Id == 1003).ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(looseAssembly);
        LogRecords(app).ShouldNotContain(record => record.Id.Id == 1000);
    }

    [Fact]
    public void Folder_without_main_assembly_fails()
    {
        var folder = testPlugins.Install(Greeter, PluginDirectory);
        File.Move(Path.Combine(folder, $"{GreeterId}.dll"), Path.Combine(folder, "Renamed.dll"));

        var exception = Should.Throw<PluginException>(() => BuildApp(builder => builder.Plugins.AddFromDirectory(PluginDirectory)));

        exception.PluginId.ShouldBe(GreeterId);
        exception.Message.ShouldContain($"{GreeterId}.dll");
        exception.Message.ShouldContain(folder);
    }

    [Fact]
    public void Folder_without_plugin_types_fails()
    {
        testPlugins.Install(NoPlugins, PluginDirectory);

        var exception = Should.Throw<PluginException>(() => BuildApp(builder => builder.Plugins.AddFromDirectory(PluginDirectory)));

        exception.PluginId.ShouldBe(NoPluginsId);
        exception.Message.ShouldContain("contains no public plugin type");
    }

    [Fact]
    public void Folder_with_an_invalid_assembly_fails()
    {
        var folder = Path.Combine(PluginDirectory, "Broken");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Broken.dll"), "not an assembly");

        var exception = Should.Throw<PluginException>(() => BuildApp(builder => builder.Plugins.AddFromDirectory(PluginDirectory)));

        exception.PluginId.ShouldBe("Broken");
        exception.Message.ShouldContain("could not be read");
        exception.InnerException.ShouldBeOfType<BadImageFormatException>();
    }

    [Fact]
    public void SkipAndLog_skips_broken_folders_and_loads_the_others()
    {
        testPlugins.Install(NoPlugins, PluginDirectory);
        testPlugins.Install(Counter, PluginDirectory);
        Directory.CreateDirectory(Path.Combine(PluginDirectory, "Empty"));

        var app = BuildApp(builder =>
        {
            builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
            builder.Plugins.AddFromDirectory(PluginDirectory);
        });

        var records = LogRecords(app);
        records.Where(record => record.Id.Id == 1001).Count().ShouldBe(2);
        records.Where(record => record.Id.Id == 1000).ShouldHaveSingleItem().Message.ShouldContain(CounterId);
    }

    [Fact]
    public void Same_plugin_in_two_directories_fails_even_with_SkipAndLog()
    {
        var first = testPlugins.Install(Greeter, Path.Combine(_directory.Path, "first"));
        var second = testPlugins.Install(Greeter, Path.Combine(_directory.Path, "second"));

        var exception = Should.Throw<PluginException>(() => BuildApp(builder =>
        {
            builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
            builder.Plugins.AddFromDirectory("first").AddFromDirectory("second");
        }));

        exception.PluginId.ShouldBe(GreeterId);
        exception.Message.ShouldContain(first);
        exception.Message.ShouldContain(second);
    }

    [Fact]
    public void Plugin_added_in_code_and_found_in_a_directory_fails()
    {
        // The test assembly itself is a plugin assembly with ID "Plugwright.IntegrationTests".
        var folder = Path.Combine(PluginDirectory, "Plugwright.IntegrationTests");
        Directory.CreateDirectory(folder);
        File.Copy(typeof(InCodePlugin).Assembly.Location, Path.Combine(folder, "Plugwright.IntegrationTests.dll"));

        var exception = Should.Throw<PluginException>(() => BuildApp(builder =>
        {
            builder.Plugins.Add<InCodePlugin>();
            builder.Plugins.AddFromDirectory(PluginDirectory);
        }));

        exception.Message.ShouldContain("provided twice");
        exception.Message.ShouldContain("added in code");
        exception.Message.ShouldContain(folder);
    }

    private IHost BuildApp(Action<HostApplicationBuilder> configure, IDictionary<string, string?>? settings = null)
    {
        var builder = TestHost.CreateBuilder(_directory.Path, settings);
        configure(builder);
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }

    private static IReadOnlyList<FakeLogRecord> LogRecords(IHost app) => app.Services.GetFakeLogCollector().GetSnapshot();

}

/// <summary>
/// A plugin in the test assembly, used to provoke a duplicate plugin ID.
/// </summary>
public sealed class InCodePlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
    }
}
