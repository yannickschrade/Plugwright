using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class PhaseGuardTests
{
    [Fact]
    public void UsePlugins_without_ApplyPlugins_throws()
    {
        var app = TestHost.CreateBuilder().Build();

        var exception = Should.Throw<InvalidOperationException>(() => app.UsePlugins());

        exception.Message.ShouldContain("ApplyPlugins()");
    }

    [Fact]
    public void ApplyPlugins_twice_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.ApplyPlugins();

        Should.Throw<InvalidOperationException>(() => builder.ApplyPlugins());
    }

    [Fact]
    public void UsePlugins_twice_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();

        Should.Throw<InvalidOperationException>(() => app.UsePlugins());
    }

    [Fact]
    public void Adding_a_plugin_after_ApplyPlugins_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.ApplyPlugins();

        var exception = Should.Throw<InvalidOperationException>(() => builder.Plugins.Add<MarkerPlugin>());

        exception.Message.ShouldContain("ApplyPlugins()");
    }

    [Fact]
    public void Adding_a_directory_after_ApplyPlugins_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.ApplyPlugins();

        Should.Throw<InvalidOperationException>(() => builder.Plugins.AddFromDirectory("plugins"));
    }

    [Fact]
    public void Adding_a_handler_after_ApplyPlugins_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.ApplyPlugins();

        Should.Throw<InvalidOperationException>(() => builder.PluginHandlers.Add<RecordingHandler>());
    }

    [Fact]
    public void Plugins_returns_the_same_collection_for_the_same_builder()
    {
        var builder = TestHost.CreateBuilder();

        builder.Plugins.ShouldBeSameAs(builder.Plugins);
        builder.PluginHandlers.ShouldBeSameAs(builder.PluginHandlers);
    }
}
