using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Plugwright.Scanned.Plugins;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class IsPluginLoadedTests
{
    [Fact]
    public void Knows_plugins_that_were_applied_before()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<AlphaPlugin>();
        builder.Plugins.Add<ContextCapturingPlugin>();

        builder.ApplyPlugins();

        var context = ContextCapturingPlugin.GetContext(builder);
        context.IsPluginLoaded("Plugwright.Scanned.Plugins").ShouldBeTrue();
        context.IsPluginLoaded("plugwright.scanned.plugins").ShouldBeTrue();
        context.IsPluginLoaded("Acme.Missing").ShouldBeFalse();
    }

    [Fact]
    public void Context_created_in_a_unit_test_knows_no_plugins()
    {
        var context = new PluginContext(
            Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings()),
            new ConfigurationBuilder().Build(),
            new PluginMetadata("Acme.Audit", "1.0.0"),
            NullLogger.Instance);

        context.IsPluginLoaded("Acme.Logging").ShouldBeFalse();
        Should.Throw<ArgumentException>(() => context.IsPluginLoaded(" "));
    }
}
