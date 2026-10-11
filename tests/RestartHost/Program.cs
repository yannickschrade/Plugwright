using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;

var mode = args[0];
var logFile = args[1];
var run = File.Exists(logFile) ? "second" : "first";
Log("start");

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
builder.Plugins.Options.SettingsFile = null;
builder.Plugins.Options.RestartMode = mode == "relaunch" ? ApplicationRestartMode.Relaunch : ApplicationRestartMode.ExitCode;
builder.ApplyPlugins();
Log("applied");

var app = builder.Build();
app.UsePlugins();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    if (run == "first")
    {
        app.Services.GetRequiredService<IApplicationRestarter>().RequestRestart();
    }
    else
    {
        lifetime.StopApplication();
    }
});

await app.RunAsync();
Log("end");

void Log(string step)
{
    // Two processes can write at the same time; retry briefly instead of failing.
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            File.AppendAllText(logFile, $"{run}:{step}{Environment.NewLine}");
            return;
        }
        catch (IOException) when (attempt < 50)
        {
            Thread.Sleep(20);
        }
    }
}
