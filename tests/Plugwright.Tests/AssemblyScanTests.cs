using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright.Scanned.Contracts;
using Plugwright.Scanned.Plugins;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

/// <summary>
/// Plugins from assemblies of the application: Plugwright.Scanned.Plugins has two plugins,
/// Plugwright.Scanned.Contracts is referenced but has none.
/// </summary>
public sealed class AssemblyScanTests
{
    private const string ScannedId = "Plugwright.Scanned.Plugins";

    [Fact]
    public void AddFromAssembly_adds_all_plugin_types_of_the_assembly()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.AddFromAssembly(typeof(AlphaPlugin).Assembly).ShouldBeSameAs(builder.Plugins);

        var app = Build(builder);

        app.Services.GetServices<ScannedMarker>().Select(marker => marker.Source).Order()
            .ShouldBe([nameof(AlphaPlugin), nameof(BetaPlugin)]);
        var plugin = app.Services.GetRequiredService<IPluginCatalog>().Find(ScannedId).ShouldNotBeNull();
        plugin.State.ShouldBe(PluginState.Loaded);
        plugin.Version.ShouldBe("1.4.0");
        plugin.Source.ShouldBe("added in code");
    }

    [Fact]
    public void AddFromReferencedAssemblies_finds_referenced_assemblies_by_prefix()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.AddFromReferencedAssemblies("plugwright.scanned").ShouldBeSameAs(builder.Plugins);

        var app = Build(builder);

        app.Services.GetServices<ScannedMarker>().Count().ShouldBe(2);
        // The contracts assembly matches the prefix but has no plugins: it is skipped, not a failure.
        app.Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldHaveSingleItem().Id.ShouldBe(ScannedId);
    }

    [Fact]
    public void AddFromReferencedAssemblies_matches_only_at_a_dot()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.AddFromReferencedAssemblies("Plugwright.Scan");

        var app = Build(builder);

        app.Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldBeEmpty();
    }

    [Fact]
    public void Types_added_in_code_and_found_by_the_scan_are_applied_once()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<AlphaPlugin>();
        builder.Plugins.AddFromReferencedAssemblies("Plugwright.Scanned");

        var app = Build(builder);

        app.Services.GetServices<ScannedMarker>().Select(marker => marker.Source).Order()
            .ShouldBe([nameof(AlphaPlugin), nameof(BetaPlugin)]);
    }

    [Fact]
    public void Scanned_plugins_can_be_turned_off()
    {
        var builder = TestHost.CreateBuilder(new Dictionary<string, string?> { [$"Plugwright:Plugins:{ScannedId}:Enabled"] = "false" });
        builder.Plugins.AddFromReferencedAssemblies("Plugwright.Scanned");

        var app = Build(builder);

        app.Services.GetServices<ScannedMarker>().ShouldBeEmpty();
        app.Services.GetRequiredService<IPluginCatalog>().Find(ScannedId).ShouldNotBeNull().State.ShouldBe(PluginState.Disabled);
    }

    [Fact]
    public void AddFromAssembly_without_plugin_types_fails()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.AddFromAssembly(typeof(IScannedContract).Assembly);

        var exception = Should.Throw<PluginException>(builder.ApplyPlugins);

        exception.Message.ShouldContain("Assembly 'Plugwright.Scanned.Contracts' contains no public plugin type");
    }

    [Fact]
    public void AddFromAssembly_without_plugin_types_is_skipped_with_SkipAndLog()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.AddFromAssembly(typeof(IScannedContract).Assembly);

        var app = Build(builder);

        app.Services.GetRequiredService<IPluginCatalog>().Find("Plugwright.Scanned.Contracts").ShouldNotBeNull()
            .State.ShouldBe(PluginState.NoHandler);
    }

    [Fact]
    public void Adding_the_same_assembly_twice_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.AddFromAssembly(typeof(AlphaPlugin).Assembly);

        Should.Throw<ArgumentException>(() => builder.Plugins.AddFromAssembly(typeof(AlphaPlugin).Assembly));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".Acme")]
    [InlineData("Acme.")]
    public void AddFromReferencedAssemblies_with_an_invalid_prefix_throws(string prefix)
    {
        var builder = TestHost.CreateBuilder();

        Should.Throw<ArgumentException>(() => builder.Plugins.AddFromReferencedAssemblies(prefix));
    }

    [Fact]
    public void Sources_describe_themselves()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.AddFromAssembly(typeof(MarkerPlugin).Assembly).AddFromReferencedAssemblies("Acme.Plugins");

        builder.Plugins.Select(source => source.ToString()).ShouldBe(["Assembly Plugwright.Tests", "Referenced assemblies Acme.Plugins"]);
    }

    [Fact]
    public void No_verification_notice_for_scanned_assemblies()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.AddFromAssembly(typeof(AlphaPlugin).Assembly);

        var app = Build(builder);

        app.Services.GetFakeLogCollector().GetSnapshot().ShouldNotContain(record => record.Id.Id == 1004);
    }

    private static IHost Build(HostApplicationBuilder builder)
    {
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }
}
