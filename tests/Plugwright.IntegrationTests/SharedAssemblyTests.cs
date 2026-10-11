using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright.TestPlugins.TextLib;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Assemblies the host shares with plugins: the host references TextLib 2.0, the Greeter plugin ships TextLib 1.0.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class SharedAssemblyTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Without_sharing_the_plugin_uses_its_own_copy()
    {
        testPlugins.Install(Greeter, PluginDirectory);

        var app = BuildApp(builder => { });

        TextLibVersion(app).ShouldBe("1");
        PluginAssemblyNames(app).ShouldContain("Plugwright.TestPlugins.TextLib");
    }

    [Fact]
    public void Shared_assembly_comes_from_the_host_even_when_the_plugin_ships_it()
    {
        testPlugins.Install(Greeter, PluginDirectory);

        var app = BuildApp(builder => builder.Plugins.ShareAssembly(typeof(TextInfo).Assembly));

        TextLibVersion(app).ShouldBe(TextInfo.Version);
        TextInfo.Version.ShouldBe("2");
        PluginAssemblyNames(app).ShouldNotContain("Plugwright.TestPlugins.TextLib");
    }

    [Fact]
    public void Prefix_shares_the_assembly_with_that_name()
    {
        testPlugins.Install(Greeter, PluginDirectory);

        var app = BuildApp(builder => builder.Plugins.ShareAssemblies("Plugwright.TestPlugins"));

        TextLibVersion(app).ShouldBe("2");
    }

    [Fact]
    public void Prefix_matches_only_at_a_dot()
    {
        testPlugins.Install(Greeter, PluginDirectory);

        var app = BuildApp(builder => builder.Plugins.ShareAssemblies("Plugwright.TestPlugins.Text"));

        TextLibVersion(app).ShouldBe("1");
    }

    private IHost BuildApp(Action<HostApplicationBuilder> configure)
    {
        var builder = TestHost.CreateBuilder(_directory.Path);
        builder.Plugins.AddFromDirectory(PluginDirectory);
        configure(builder);
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }

    private static string? TextLibVersion(IHost app) =>
        app.Services.GetRequiredService<IConfiguration>()["TestPlugins:Greeter:TextLibVersion"];

    // The assemblies in the load context of this app's Greeter plugin.
    private static List<string?> PluginAssemblyNames(IHost app)
    {
        var greeter = app.Services.GetRequiredService<PluginReport>().Plugins.First(plugin => plugin.Metadata.Id == GreeterId);
        return AssemblyLoadContext.GetLoadContext(greeter.PluginType.Assembly)!.Assemblies
            .Select(assembly => assembly.GetName().Name)
            .ToList();
    }
}
