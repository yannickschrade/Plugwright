using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class PluginCollectionTests
{
    [Fact]
    public void Enumerates_added_sources_in_order()
    {
        var builder = TestHost.CreateBuilder();

        builder.Plugins.Add<SecondMarkerPlugin>().AddFromDirectory("plugins").Add<MarkerPlugin>();

        builder.Plugins.Count.ShouldBe(3);
        builder.Plugins.Select(source => source.ToString()).ShouldBe(
        [
            $"Type {typeof(SecondMarkerPlugin).FullName}",
            "Directory plugins",
            $"Type {typeof(MarkerPlugin).FullName}",
        ]);
        builder.Plugins.OfType<PluginTypeSource>().Select(source => source.PluginType)
            .ShouldBe([typeof(SecondMarkerPlugin), typeof(MarkerPlugin)]);
        builder.Plugins.OfType<PluginDirectorySource>().ShouldHaveSingleItem().Path.ShouldBe("plugins");
    }

    [Fact]
    public void Add_returns_the_collection_for_chaining()
    {
        var builder = TestHost.CreateBuilder();

        builder.Plugins.Add<MarkerPlugin>().ShouldBeSameAs(builder.Plugins);
        builder.Plugins.AddFromDirectory("plugins").ShouldBeSameAs(builder.Plugins);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddFromDirectory_with_an_empty_path_throws(string path)
    {
        var builder = TestHost.CreateBuilder();

        Should.Throw<ArgumentException>(() => builder.Plugins.AddFromDirectory(path));
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
