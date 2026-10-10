# Getting started

In this tutorial you build a console application that loads a plugin from a plugin directory, and you make sure it only loads the plugin files you approved. It takes about ten minutes.

The finished code is in `samples/GettingStarted` in the repository.

You need the .NET 10 SDK.

## 1. Create the host application

Create a console application and add Plugwright:

```bash
dotnet new console -n MyApp
dotnet add MyApp package Microsoft.Extensions.Hosting
dotnet add MyApp package Plugwright
```

## 2. Create a plugin

A plugin is a class library with a class that implements [`IPlugin`](../api/Plugwright.IPlugin.md):

```bash
dotnet new classlib -n Acme.Hello
dotnet add Acme.Hello package Plugwright.Abstractions
```

Replace the generated class with a plugin that adds a hosted service:

```csharp
--8<-- "samples/GettingStarted/Acme.Hello/HelloPlugin.cs:plugin"
```

Open `Acme.Hello.csproj` and make two changes, so that the build output can be deployed as a plugin folder:

```xml
--8<-- "samples/GettingStarted/Acme.Hello/Acme.Hello.csproj"
```

`EnableDynamicLoading` copies all dependencies into the output. `ExcludeAssets="runtime"` keeps `Plugwright.Abstractions.dll` out of it, because the host provides that assembly. When you reference the package instead of the project, put `ExcludeAssets="runtime"` on the `PackageReference`.

## 3. Load plugins and verify them

Replace `Program.cs` of the host application:

```csharp
--8<-- "samples/GettingStarted/App/Program.cs:host"
```

- `AddFromDirectory` loads every plugin from its own folder in `plugins`.
- `AddHashVerifier` only loads plugins whose files match a lock file. Verification is optional: without it, Plugwright loads whatever is in the plugin directory and logs a notice.
- `ApplyPlugins` and `UsePlugins` run the two phases of the plugin system. [The two phases](../explanation/two-phases.md) explains them.

## 4. Deploy the plugin

Build the application, then publish the plugin into its folder next to the application:

```bash
dotnet build MyApp
dotnet publish Acme.Hello -o MyApp/bin/Debug/net10.0/plugins/Acme.Hello
```

## 5. Create the lock file

The lock file lists the hash of every file of every plugin. Create it with the `plugwright` tool:

```bash
dotnet tool install --global Plugwright.Cli
plugwright lock MyApp/bin/Debug/net10.0/plugins --output MyApp/bin/Debug/net10.0/plugins.lock.json
```

## 6. Run the application

```bash
dotnet run --project MyApp --no-build
```

Plugwright verifies the plugin, loads it, and the plugin's service says hello:

```text
info: Plugwright[1005]
      Plugin Acme.Hello (.../plugins/Acme.Hello) passed HashVerifier: The hashes of all 4 files match the lock file '.../plugins.lock.json'.
info: Plugwright[1000]
      Plugin Acme.Hello 0.1.0 loaded (Acme.Hello.HelloPlugin, .../plugins/Acme.Hello)
info: Acme.Hello.HelloService[0]
      Hello from a verified plugin!
```

## 7. See verification fail

Change a plugin file, for example by appending a byte to `Acme.Hello.dll`, and run the application again. It stops before any plugin code is loaded:

```text
Unhandled exception. Plugwright.PluginException: Plugin 'Acme.Hello' (.../plugins/Acme.Hello): Verification by HashVerifier failed: File 'Acme.Hello.dll' does not match its hash in the lock file.
```

Whenever you install or update a plugin, create the lock file again.

## Next steps

- [Verify plugins with a lock file](../how-to/verify-plugins-with-a-lock-file.md), including where to keep the lock file in production.
- [Sign plugins and trust their publisher](../how-to/sign-plugins.md), so that plugin updates need no change on the host.
- [Configure plugins](../how-to/add-a-plugin.md#read-settings) with their own settings section.
- [Declare dependencies between plugins](../how-to/declare-plugin-dependencies.md).
- [Security model](../explanation/security-model.md): what verification protects against, and what not.
