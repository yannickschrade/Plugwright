using Microsoft.Extensions.DependencyInjection;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class HandlerPipelineTests
{
    [Fact]
    public void All_matching_handlers_run_default_handler_first_and_share_one_instance()
    {
        var builder = TestHost.CreateBuilder();
        builder.PluginHandlers.Add<RecordingHandler>();
        builder.Plugins.Add<TaggedPlugin>();

        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();

        var calls = app.Services.GetRequiredService<CallLog>().Calls;
        calls.Count.ShouldBe(3);
        calls.Select(call => call.Split(':')[0]).ShouldBe(["Configure", "Apply", "Use"]);
        calls.Select(call => call.Split(':')[1]).Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public void Handler_without_Use_relies_on_the_default_implementation()
    {
        var builder = TestHost.CreateBuilder();
        builder.PluginHandlers.Add<ApplyOnlyHandler>();
        builder.Plugins.Add<TaggedPlugin>();

        builder.ApplyPlugins();
        var app = builder.Build();

        Should.NotThrow(() => app.UsePlugins());
    }

    [Fact]
    public void Handler_only_receives_types_it_can_handle()
    {
        var builder = TestHost.CreateBuilder();
        builder.PluginHandlers.Add<RecordingHandler>();
        builder.Plugins.Add<MarkerPlugin>().Add<TaggedPlugin>();

        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();

        var taggedInstanceId = app.Services.GetRequiredService<CallLog>().Calls[0].Split(':')[1];
        app.Services.GetRequiredService<CallLog>().Calls.ShouldAllBe(call => call.EndsWith(taggedInstanceId));
    }

    [Fact]
    public void Error_in_Use_throws_PluginException_naming_the_plugin()
    {
        var builder = TestHost.CreateBuilder();
        builder.PluginHandlers.Add<ThrowingUseHandler>();
        builder.Plugins.Add<TaggedPlugin>();
        builder.ApplyPlugins();
        var app = builder.Build();

        var exception = Should.Throw<PluginException>(() => app.UsePlugins());

        exception.PluginId.ShouldBe("Plugwright.Tests");
        exception.Message.ShouldContain(nameof(ThrowingUseHandler));
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Plugin_type_without_handler_needs_a_custom_handler()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<UnhandledPlugin>();

        var exception = Should.Throw<PluginException>(() => builder.ApplyPlugins());

        exception.Message.ShouldContain("PluginHandlers.Add");
        exception.Message.ShouldContain(nameof(UnhandledPlugin));
    }
}
