using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WorkerHealth;

// --8<-- [start:publisher]
/// <summary>
/// Writes the health status to a file after every run of the health checks, for example for a Kubernetes exec probe:
/// <c>grep -q Healthy /app/health.txt</c>.
/// </summary>
public sealed class FileHealthPublisher(string path) : IHealthCheckPublisher
{
    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        var lines = report.Entries.Select(entry => $"{entry.Key}: {entry.Value.Status} {entry.Value.Description}");
        await File.WriteAllLinesAsync(path, [report.Status.ToString(), .. lines], cancellationToken);
    }
}
// --8<-- [end:publisher]
