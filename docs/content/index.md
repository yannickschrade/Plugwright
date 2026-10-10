# Plugwright

Plugwright adds a plugin system to the .NET Generic Host. Plugins extend a host application through dependency injection: they register services, hosted services and configuration, just like the application itself.

Plugwright works with any [`IHostApplicationBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostapplicationbuilder) and [`IHost`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihost), so console apps, worker services and web apps all use it the same way.

```csharp
--8<-- "samples/Heartbeat/Program.cs:host"
```

## Where to go next

- [Getting started](tutorials/getting-started.md): load a verified plugin in ten minutes
- [Add a plugin to your application](how-to/add-a-plugin.md)
- [Use plugins in an ASP.NET Core app](how-to/use-in-a-web-app.md) and [add Blazor pages from plugins](how-to/add-blazor-pages.md)
- [Load plugins from a directory](how-to/load-plugins-from-a-directory.md) or [from NuGet packages](how-to/load-plugins-from-packages.md)
- [Declare dependencies between plugins](how-to/declare-plugin-dependencies.md)
- [Turn plugins on and off](how-to/enable-and-disable-plugins.md) and [make plugins required](how-to/required-and-optional-plugins.md)
- [Run startup tasks](how-to/run-startup-tasks.md) before the application starts
- [Inspect plugins at runtime](how-to/inspect-plugins-at-runtime.md)
- [Support your own kind of plugin with a handler](how-to/write-a-plugin-handler.md)
- [Understand the two phases](explanation/two-phases.md)

!!! warning "Important"

    Plugins run with full trust inside your process. A plugin can do anything your application can do. Only load plugins you trust, and [verify plugins](how-to/verify-plugins-with-a-lock-file.md) loaded from a directory. Verification is optional and off by default.
