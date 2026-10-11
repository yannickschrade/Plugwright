using Microsoft.Extensions.DependencyInjection;

namespace Plugwright.Tests;

/// <summary>
/// The registration of <see cref="IApplicationRestarter"/>. Restarts themselves run in their own process, see
/// RestartTests in Plugwright.IntegrationTests.
/// </summary>
public sealed class ApplicationRestarterTests
{
    [Fact]
    public void ApplyPlugins_registers_the_built_in_restarter()
    {
        var builder = TestHost.CreateBuilder();
        builder.ApplyPlugins();

        builder.Build().Services.GetRequiredService<IApplicationRestarter>().ShouldBeOfType<ApplicationRestarter>();
    }

    [Fact]
    public void Own_restarter_registered_before_ApplyPlugins_wins()
    {
        var builder = TestHost.CreateBuilder();
        builder.Services.AddSingleton<IApplicationRestarter, OwnRestarter>();
        builder.ApplyPlugins();

        builder.Build().Services.GetRequiredService<IApplicationRestarter>().ShouldBeOfType<OwnRestarter>();
    }

    [Fact]
    public void Own_restarter_registered_after_ApplyPlugins_wins()
    {
        var builder = TestHost.CreateBuilder();
        builder.ApplyPlugins();
        builder.Services.AddSingleton<IApplicationRestarter, OwnRestarter>();

        builder.Build().Services.GetRequiredService<IApplicationRestarter>().ShouldBeOfType<OwnRestarter>();
    }

    [Fact]
    public void Defaults_restart_with_exit_code_75()
    {
        var options = new PluginOptions();

        options.RestartMode.ShouldBe(ApplicationRestartMode.ExitCode);
        options.RestartExitCode.ShouldBe(75);
    }

    private sealed class OwnRestarter : IApplicationRestarter
    {
        public void RequestRestart()
        {
        }
    }
}
