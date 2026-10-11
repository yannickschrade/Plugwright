# Restart the application

Turning plugins on or off, and installing or updating them, takes effect at the next start. [`IApplicationRestarter`](../api/Plugwright.IApplicationRestarter.md) lets an administration page or a plugin trigger that start, without knowing how the application is run. How it restarts is decided by the host application.

## Ask for a restart

`builder.ApplyPlugins()` registers `IApplicationRestarter`. Inject it and call [`RequestRestart()`](../api/Plugwright.IApplicationRestarter.md#Plugwright_IApplicationRestarter_RequestRestart). The page in `samples/BlazorHost` offers it after a plugin was turned on or off:

```razor
--8<-- "samples/BlazorHost/App/Components/Pages/Plugins.razor:restart"
```

`RequestRestart()` stops the application gracefully, like `IHostApplicationLifetime.StopApplication()`: hosted services stop, requests in progress complete. It returns immediately, and calling it again has no effect. The log event `ApplicationRestartRequested` (1012) records the request.

## Choose how the application restarts

Set [`PluginOptions.RestartMode`](../api/Plugwright.PluginOptions.md#Plugwright_PluginOptions_RestartMode) before `builder.ApplyPlugins()`:

| [`ApplicationRestartMode`](../api/Plugwright.ApplicationRestartMode.md) | What happens | For |
| --- | --- | --- |
| `ExitCode` (default) | The application stops with exit code 75. The service manager starts it again. | Services and containers: systemd, Windows services, Docker, Kubernetes, IIS |
| `Relaunch` | The application starts a new process of itself with the same executable and arguments, then stops. | Console and desktop applications that nobody restarts |

```csharp
builder.Plugins.Options.RestartMode = ApplicationRestartMode.Relaunch;
```

To restart in another way, for example through an updater or launcher, register your own implementation. It replaces the built-in one, whether you register it before or after `builder.ApplyPlugins()`:

```csharp
builder.Services.AddSingleton<IApplicationRestarter, LauncherRestarter>();
```

## Restart with an exit code

In `ExitCode` mode, the application only stops. Whether it starts again depends on what runs it:

| Environment | Setting |
| --- | --- |
| systemd | `Restart=on-failure` or `Restart=always`. With `RestartForceExitStatus=75` and `Restart=no`, the service restarts only for this exit code. |
| Windows service | Recovery actions in the service properties, with *Enable actions for stops with errors* checked. |
| Docker | `--restart on-failure`, `always` or `unless-stopped`. |
| Kubernetes | Restarts containers by default (`restartPolicy: Always`). |
| IIS | The ASP.NET Core Module starts the application again with the next request, in-process and out-of-process. The exit code does not matter. |
| Console, `dotnet run`, local .NET Aspire | Nothing restarts the application. Use `Relaunch`, or start it again yourself. |

The exit code is not 0, so that managers which restart only after failures restart the application as well. Change it with [`PluginOptions.RestartExitCode`](../api/Plugwright.PluginOptions.md#Plugwright_PluginOptions_RestartExitCode). It is set with `Environment.ExitCode`, so it only applies when `Main` returns `void` or `Task`, as with top-level statements and `await app.RunAsync()`. A `Main` that returns an `int` decides the exit code itself.

## Restart by starting a new process

In `Relaunch` mode, the application starts the new process when its services have stopped, for example when the web server has released its ports. The new process then waits in `builder.ApplyPlugins()`, before any plugin is loaded, until the old process has exited, for at most 30 seconds. So the two never run plugins at the same time, and files the old process held are free.

The new process gets the same executable, arguments, working directory and environment. Things to keep in mind:

- Do not use `Relaunch` under a service manager. It would only see that the application stopped, and could start another instance next to the new process.
- In a terminal, the new process runs in the background of the shell: the prompt returns, and `Ctrl+C` no longer reaches the application. Stop it with `kill` or the Task Manager, or use `Relaunch` only for applications without a console.
- The new process is not attached to a debugger.

## See also

- [Turn plugins on and off](enable-and-disable-plugins.md)
- [Use Plugwright with .NET Aspire](use-with-aspire.md)
