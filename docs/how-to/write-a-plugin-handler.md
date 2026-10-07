# Support your own kind of plugin with a handler

`builder.Plugins.Add<T>()` only records a plugin type. A *plugin handler* decides what happens with it. The built-in handler processes every type that implements <xref:Plugwright.IPlugin>. Write your own handler when your application defines a different kind of plugin, for example plugins that contribute menu entries, report types or greetings.

The complete, runnable project is in `samples/CustomHandler` in the repository.

## Define the plugin contract

The host application defines what its plugins look like. Here, a plugin returns greetings:

[!code-csharp[](../../samples/CustomHandler/IGreetingPlugin.cs?name=contract)]

[!code-csharp[](../../samples/CustomHandler/Greetings.cs?name=plugins)]

## Write the handler

A handler implements <xref:Plugwright.IPluginHandler>:

- <xref:Plugwright.IPluginHandler.CanHandle(System.Type)> returns whether the handler processes a plugin type.
- <xref:Plugwright.IPluginHandler.Apply(Plugwright.PluginDescriptor,Microsoft.Extensions.Hosting.IHostApplicationBuilder)> runs in phase 1, before the host is built. Register services here.
- <xref:Plugwright.IPluginHandler.Use(Plugwright.PluginDescriptor,Microsoft.Extensions.Hosting.IHost)> runs in phase 2, after the host is built. It is optional; the default implementation does nothing.

[!code-csharp[](../../samples/CustomHandler/GreetingPluginHandler.cs?name=handler)]

The <xref:Plugwright.PluginDescriptor> gives the handler the plugin's type, its instance, its metadata and its <xref:Plugwright.PluginContext>.

## Register the handler

Register the handler with `builder.PluginHandlers.Add<T>()` before calling `ApplyPlugins()`:

[!code-csharp[](../../samples/CustomHandler/Program.cs?name=host)]

## Rules for handlers

- Handlers are created with their parameterless constructor, not resolved from dependency injection. The service container does not exist yet in phase 1.
- Every handler whose `CanHandle` returns `true` processes the plugin, in registration order. The built-in handler for `IPlugin` always runs first. A plugin can therefore implement `IPlugin` and your own interface, and both handlers process it.
- Plugwright creates exactly one instance of each plugin type. All handlers receive the same instance in both phases.
- A plugin type that no handler can handle causes a <xref:Plugwright.PluginException>, unless [failures are skipped](handle-plugin-failures.md).
- Handlers come from the host application. Plugins cannot register handlers.
