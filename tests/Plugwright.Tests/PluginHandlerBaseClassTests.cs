using Microsoft.Extensions.DependencyInjection;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class PluginHandlerBaseClassTests
{
    [Theory]
    [InlineData(typeof(TaggedPlugin), true)]
    [InlineData(typeof(MarkerPlugin), false)]
    [InlineData(typeof(UnhandledPlugin), false)]
    public void CanHandle_matches_types_assignable_to_the_type_argument(Type pluginType, bool expected)
    {
        new TypedRecordingHandler().CanHandle(pluginType).ShouldBe(expected);
    }

    [Fact]
    public void Apply_and_Use_receive_the_typed_plugin_instance()
    {
        var builder = TestHost.CreateBuilder();
        builder.PluginHandlers.Add<TypedRecordingHandler>();
        builder.Plugins.Add<TaggedPlugin>();

        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();

        app.Services.GetRequiredService<CallLog>().Calls
            .Where(call => call.StartsWith("Typed", StringComparison.Ordinal))
            .ShouldBe(["TypedApply:tagged:True", "TypedUse:tagged:True"]);
    }

    [Fact]
    public void Use_does_nothing_unless_overridden()
    {
        var builder = TestHost.CreateBuilder();
        builder.PluginHandlers.Add<TypedApplyOnlyHandler>();
        builder.Plugins.Add<TaggedPlugin>();

        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();

        app.Services.GetRequiredService<CallLog>().Calls.ShouldContain("TypedApplyOnly");
    }
}
