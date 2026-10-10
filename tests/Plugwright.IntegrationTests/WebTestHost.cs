using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Starts tests/TestWebHost in memory with the given settings, for example the plugin directory.
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
    }
}
