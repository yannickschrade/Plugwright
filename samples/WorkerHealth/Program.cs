using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Plugwright;
using WorkerHealth;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
builder.Plugins.Add<ReportsPlugin>();

// Health checks work without a web server: a hosted service runs them periodically and hands the result to the
// registered publishers.
builder.Services.AddHealthChecks().AddPlugins();
builder.Services.AddSingleton<IHealthCheckPublisher>(new FileHealthPublisher(Path.Combine(AppContext.BaseDirectory, "health.txt")));
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay = TimeSpan.FromSeconds(2);    // first run after the start
    options.Period = TimeSpan.FromSeconds(30);  // then every 30 seconds
});

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

await app.RunAsync();
// --8<-- [end:host]
