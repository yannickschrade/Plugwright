# Split a plugin across an API host and a UI host

In an application with an API host and a separate Blazor host, one feature usually comes as three projects: a backend plugin in the API host, a UI plugin in the Blazor host, and a shared project with the DTOs between them. For Plugwright, backend and UI are two plugins in two processes.

The complete, runnable solution is in `samples/SplitHosts` in the repository.

## Lay out the projects

| Project | Kind | Loaded by |
| --- | --- | --- |
| `Acme.Orders` | Class library, plugin with services and API | API host |
| `Acme.Orders.Ui` | Razor class library, plugin with pages | Blazor host |
| `Acme.Orders.Abstractions` | Class library with DTOs and routes, no plugin | both, as a normal reference |

Give the two plugins their own IDs. The convention `<id>` for the backend and `<id>.Ui` for the UI keeps them together; Plugwright does not enforce it.

## Share the DTOs

The abstractions project contains only what both sides need:

```csharp
--8<-- "samples/SplitHosts/Acme.Orders.Abstractions/OrderDto.cs"
```

It is not a plugin. Both plugin projects reference it like any library.

## Write the backend plugin

The backend plugin is a [web plugin](use-in-a-web-app.md) that maps the API:

```csharp
--8<-- "samples/SplitHosts/Acme.Orders/OrdersPlugin.cs:plugin"
```

## Write the UI plugin

The UI plugin is a [Blazor plugin](add-blazor-pages.md). It calls the API through a typed `HttpClient`. Its address is a plugin setting, with the Aspire service name as default:

```csharp
--8<-- "samples/SplitHosts/Acme.Orders.Ui/OrdersUiPlugin.cs:plugin"
```

The two hosts register the plugin of their side:

```csharp
--8<-- "samples/SplitHosts/Web/Program.cs:host"
```

## Hide the UI when the backend is off

When the backend plugin is turned off or fails in the API host, the UI plugin's pages would only show errors. The sample hides them:

1. The API host publishes its plugin catalog with `app.MapPluginCatalog()`, see [Inspect plugins at runtime](inspect-plugins-at-runtime.md#publish-the-catalog-to-other-hosts).
2. At startup, before `ApplyPlugins()`, the web host reads that catalog and turns off every UI plugin whose backend plugin is not loaded:

    ```csharp
    --8<-- "samples/SplitHosts/Web/BackendPlugins.cs:backend"
    ```

3. The menu shows the link only when the UI plugin is loaded:

    ```razor
    --8<-- "samples/SplitHosts/Web/Components/App.razor:nav"
    ```

This is application code, not part of Plugwright. The decision lasts until the next start of the web host. To try it, uncomment the line in the app host that turns off `Acme.Orders`.

### Decide what fits your application

The check couples the start of the web host to the API, so decide on these points:

- **The API is not reachable at startup.** The sample keeps the UI plugins on: an API that starts a moment later should not hide features until the next restart, and the pages show the API's error in the meantime. If showing a feature without backend is worse, turn the UI plugins off instead, or wait for the API with a timeout. With .NET Aspire, `WaitFor(api)` in the app host starts the web host only when the API runs.
- **The API changes after the web host started.** If the API restarts with the backend plugin turned off, the web host still shows the UI plugin until its own next start. If that matters, decide at runtime instead: query the API's catalog when rendering the menu, with a short cache, and hide the link or show a notice. The UI plugin then stays loaded, and only its visibility changes.
- **Service discovery is not available before `Build()`.** The sample reads the API's address from configuration, where Aspire puts it. Without Aspire, configure the address yourself.

Plugwright leaves these decisions to the application, because the right answer depends on how the hosts are run.

## See also

- [Use Plugwright with .NET Aspire](use-with-aspire.md)
- [One plugin, several kinds](combine-plugin-kinds.md), for a plugin that runs in a single host
- [Develop plugins in a solution](develop-plugins-in-a-solution.md)
