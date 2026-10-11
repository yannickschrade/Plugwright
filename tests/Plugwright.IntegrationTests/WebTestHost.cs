using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Starts tests/TestWebHost in memory with the given settings, for example the plugin directory. Log entries are
/// collected with fake logging.
/// </summary>
internal sealed class WebTestHost(IDictionary<string, string?> settings)
    : WebApplicationFactory<Plugwright.TestWebHost.Components.App>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach ((var key, var value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services => services.AddLogging(logging => logging.AddFakeLogging()));
    }
}
