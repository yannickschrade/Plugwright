using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Creates host builders with their content root in a test directory, without reading any files, collecting all log entries.
/// </summary>
internal static class TestHost
{
    public static HostApplicationBuilder CreateBuilder(string contentRoot, IDictionary<string, string?>? settings = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = contentRoot });
        builder.Configuration.AddInMemoryCollection(settings ?? new Dictionary<string, string?>());
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddFakeLogging();
        return builder;
    }
}
