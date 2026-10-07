using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acme.Greeter;

public sealed class GreeterService(IOptions<GreeterOptions> options, ILogger<GreeterService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{Message}", options.Value.Message);
        return Task.CompletedTask;
    }
}
