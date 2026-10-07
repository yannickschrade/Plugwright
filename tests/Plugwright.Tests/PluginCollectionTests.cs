using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class PluginCollectionTests
{
    [Fact]
    public void Add_records_plugin_types_in_order()
    {
        var builder = TestHost.CreateBuilder();

        builder.Plugins.Add<SecondMarkerPlugin>().Add<MarkerPlugin>();

        builder.Plugins.ShouldBe([typeof(SecondMarkerPlugin), typeof(MarkerPlugin)]);
    }

    [Fact]
    public void Adding_the_same_type_twice_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<MarkerPlugin>();

        Should.Throw<ArgumentException>(() => builder.Plugins.Add<MarkerPlugin>());
    }

    [Theory]
    [InlineData(typeof(AbstractPlugin))]
    [InlineData(typeof(IPlugin))]
    [InlineData(typeof(PluginWithoutParameterlessConstructor))]
    [InlineData(typeof(List<>))]
    public void Adding_an_invalid_type_throws(Type pluginType)
    {
        var builder = TestHost.CreateBuilder();

        Should.Throw<ArgumentException>(() => builder.Plugins.Add(pluginType));
    }

    [Fact]
    public void Adding_the_same_handler_twice_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.PluginHandlers.Add<RecordingHandler>();

        Should.Throw<ArgumentException>(() => builder.PluginHandlers.Add<RecordingHandler>());
    }

    [Fact]
    public void Failure_mode_defaults_to_fail_fast()
    {
        TestHost.CreateBuilder().Plugins.Options.FailureMode.ShouldBe(PluginFailureMode.FailFast);
    }
}
