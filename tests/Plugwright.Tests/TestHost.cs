using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Plugwright.Tests;

/// <summary>
/// Creates host builders that read no files and collect all log entries.
/// </summary>
internal static class TestHost
{
    public static HostApplicationBuilder CreateBuilder(IDictionary<string, string?>? settings = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(settings ?? new Dictionary<string, string?>());
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddFakeLogging();
        return builder;
    }
}
