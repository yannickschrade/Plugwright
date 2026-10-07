---
_layout: landing
---

# Plugwright

Plugwright adds a plugin system to the .NET Generic Host. Plugins extend a host application through dependency injection: they register services, hosted services and configuration, just like the application itself.

Plugwright works with any <xref:Microsoft.Extensions.Hosting.IHostApplicationBuilder> and <xref:Microsoft.Extensions.Hosting.IHost>, so console apps, worker services and web apps all use it the same way.

[!code-csharp[](../samples/Heartbeat/Program.cs?name=host)]

## Where to go next

- [Getting started](tutorials/getting-started.md): load a verified plugin in ten minutes
- [Add a plugin to your application](how-to/add-a-plugin.md)
- [Load plugins from a directory](how-to/load-plugins-from-a-directory.md)
- [Load plugins from NuGet packages](how-to/load-plugins-from-packages.md)
- [Declare dependencies between plugins](how-to/declare-plugin-dependencies.md)
- [Support your own kind of plugin with a handler](how-to/write-a-plugin-handler.md)
- [Keep running when a plugin fails](how-to/handle-plugin-failures.md)
- [Understand the two phases](explanation/two-phases.md)

> [!IMPORTANT]
> Plugins run with full trust inside your process. A plugin can do anything your application can do. Only load plugins you trust, and [verify plugins](how-to/verify-plugins-with-a-lock-file.md) loaded from a directory. Verification is optional and off by default.
