# Plugwright

Plugwright adds a plugin system to the .NET Generic Host. Plugins extend a host application through dependency injection: they register services, hosted services and configuration, just like the application itself.

It works with any `IHostApplicationBuilder` and `IHost`, so console apps, worker services and web apps use it the same way.

> **Status:** early development. The API may change before 1.0.

## Example

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Plugins.Add<HeartbeatPlugin>();          // a plugin from your own code
builder.Plugins.AddFromDirectory("plugins");     // plugins/<Name>/<Name>.dll, one load context each
builder.PluginVerifiers.AddHashVerifier();        // optional: only load plugins matching plugins.lock.json

builder.ApplyPlugins();   // phase 1: plugins register their services
var app = builder.Build();
app.UsePlugins();         // phase 2: plugins work with the built host

await app.RunAsync();
```

A plugin implements `IPlugin`:

```csharp
public sealed class HeartbeatPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services
            .AddOptions<HeartbeatOptions>()
            .Bind(context.Configuration);   // Plugwright:Plugins:{PluginId}:Settings

        context.Builder.Services.AddHostedService<HeartbeatService>();
    }
}
```

Plugins run with full trust inside your process. Only load plugins you trust, and verify plugins loaded from a directory. Verification is optional and off by default.

## Packages

| Package | Purpose |
| --- | --- |
| `Plugwright.Abstractions` | Plugin contract. Reference it from plugin projects. |
| `Plugwright` | The plugin system. Reference it from the host application. |
| `Plugwright.NuGet` | Loads plugins from a folder of NuGet packages. |
| `Plugwright.Cli` | The `plugwright` tool. Creates lock files and signs plugins for verification. |

## Documentation

The documentation lives in [`docs/`](docs/) and is built with DocFX:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

## License

[MIT](LICENSE)
