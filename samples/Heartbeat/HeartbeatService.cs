using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Heartbeat;

#region service
public sealed class HeartbeatService(IOptionsMonitor<HeartbeatOptions> options, ILogger<HeartbeatService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var count = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            count++;
            logger.LogInformation("Heartbeat {Count}", count);
            await Task.Delay(options.CurrentValue.Interval, stoppingToken);
        }
    }
}
#endregion
