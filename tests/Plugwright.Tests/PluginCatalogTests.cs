using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class PluginCatalogTests
{
    private const string TestsId = "Plugwright.Tests";

    [Fact]
    public void Loaded_plugin_has_its_metadata_and_handlers()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<TaggedPlugin>();
        builder.PluginHandlers.Add<TypedApplyOnlyHandler>();

        var app = Build(builder);

        var plugin = app.Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldHaveSingleItem();
        plugin.Id.ShouldBe(TestsId);
        plugin.Version.ShouldNotBeNullOrEmpty();
        plugin.Source.ShouldBe("added in code");
        plugin.State.ShouldBe(PluginState.Loaded);
        plugin.Reason.ShouldBeNull();
        plugin.ErrorMessage.ShouldBeNull();
        plugin.IsRequired.ShouldBeFalse();
        plugin.AppliedHandlers.ShouldBe(["DefaultPluginHandler", "TypedApplyOnlyHandler"]);
    }

    [Fact]
    public void Plugin_that_throws_in_Configure_is_failed_with_the_error_message()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<ThrowingPlugin>();

        var plugin = Build(builder).Services.GetRequiredService<IPluginCatalog>().Find(TestsId).ShouldNotBeNull();

        plugin.State.ShouldBe(PluginState.Failed);
        plugin.Reason.ShouldNotBeNull().ShouldContain("Configure went wrong");
        plugin.ErrorMessage.ShouldBe("Configure went wrong");
    }

    [Fact]
    public void Plugin_type_without_handler_is_NoHandler()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<UnhandledPlugin>();

        var plugin = Build(builder).Services.GetRequiredService<IPluginCatalog>().Find(TestsId).ShouldNotBeNull();

        plugin.State.ShouldBe(PluginState.NoHandler);
        plugin.Reason.ShouldNotBeNull().ShouldContain("No plugin handler can handle plugin type");
    }

    [Fact]
    public void One_failing_plugin_type_fails_the_plugin_but_the_other_types_keep_running()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<ThrowingPlugin>().Add<MarkerPlugin>();

        var app = Build(builder);

        app.Services.GetRequiredService<IPluginCatalog>().Find(TestsId).ShouldNotBeNull().State.ShouldBe(PluginState.Failed);
        app.Services.GetRequiredService<PluginReport>().Plugins.ShouldHaveSingleItem().PluginType.ShouldBe(typeof(MarkerPlugin));
    }

    [Fact]
    public void Find_ignores_case_and_returns_null_for_unknown_plugins()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<MarkerPlugin>();

        var catalog = Build(builder).Services.GetRequiredService<IPluginCatalog>();

        catalog.Find("plugwright.TESTS").ShouldNotBeNull();
        catalog.Find("Acme.Unknown").ShouldBeNull();
    }

    [Fact]
    public void Catalog_is_empty_without_plugins()
    {
        Build(TestHost.CreateBuilder()).Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldBeEmpty();
    }

    [Fact]
    public void State_of_every_plugin_is_logged_at_startup()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<ThrowingPlugin>();

        var app = Build(builder);

        var record = app.Services.GetRequiredService<FakeLogCollector>().GetSnapshot()
            .Where(record => record.Id.Id == 1008).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Category.ShouldBe("Plugwright");
        record.Message.ShouldStartWith($"Plugin {TestsId} ");
        record.Message.ShouldContain("Failed. Phase 1 failed");
    }

    private static IHost Build(HostApplicationBuilder builder)
    {
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }
}
