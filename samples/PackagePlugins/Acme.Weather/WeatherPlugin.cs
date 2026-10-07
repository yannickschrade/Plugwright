using Acme.Units;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plugwright;

namespace Acme.Weather;

#region plugin
public sealed class WeatherPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddHostedService<WeatherService>();
    }
}

public sealed class WeatherService(ILogger<WeatherService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Temperature comes from the library package Acme.Units.
        logger.LogInformation("Today: 21 °C ({Fahrenheit} °F)", Temperature.ToFahrenheit(21));
        return Task.CompletedTask;
    }
}
#endregion
