using Microsoft.Extensions.Hosting;
using Plugwright;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

// Library packages that are missing from the package folder are downloaded from the feed
// and stored in the download directory for the next start.
builder.Plugins.AddFromPackages(Path.Combine(AppContext.BaseDirectory, "packages"), options =>
{
    options.Feed = Path.Combine(AppContext.BaseDirectory, "feed");   // usually https://api.nuget.org/v3/index.json
    options.DownloadDirectory = Path.Combine(AppContext.BaseDirectory, "plugin-package-downloads");
});

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

await app.RunAsync();
// --8<-- [end:host]
