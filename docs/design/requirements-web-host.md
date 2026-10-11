# Plugwright – Requirements: Web Hosts, Plugin State and Startup

Date: 2026-10-10 · Status: approved for implementation

This file extends the existing specification (`docs/SPEC.md`, as of 2026-10-06). It describes what Plugwright must additionally be able to do so that an **ASP.NET Core / Blazor Server application** with business plugins can build on it. Where this file contradicts the specification, this file applies. The deviations are listed below and are added to the specification's decision log during implementation.

## 1. Starting point

A typical target application:

- **Two separate hosts**, both with Plugwright:
  - **API host** (ASP.NET Core): REST/OData API, business logic, database, background services.
  - **Web host** (Blazor, Interactive Server): user interface, calls the API via HTTP and has no database access.
  - Optionally with .NET Aspire (`AddServiceDefaults()`), which starts both hosts.
- Business features come as plugins. A business plugin usually consists of **three projects**:
  - **Backend part** (services, API controllers), loaded in the API host
  - **Abstractions** (DTOs, interfaces), used by both
  - **UI part** (Razor class library), loaded in the web host

  For Plugwright, the backend and UI parts are two separate plugins in two processes.
- The web host must know which backend plugins are active in the API in order to hide the corresponding UI plugins.
- Plugins are included via **project reference** (`Plugins.Add<T>()`). Dynamic loading from directories comes later.
- Some plugins are **required** (the app must not start without them), others are **optional** (the app starts without them and shows their state).
- Plugins must be able to perform **asynchronous preparations** at startup, e.g. register database schemas or migrate data, before the app accepts requests.
- Plugins are **enabled and disabled** via configuration. The change takes effect after a restart.

## 2. Instructions for Claude Code

1. Read `docs/SPEC.md` and this file completely before you start.
2. Implement the milestones **in the given order**. Within a milestone, the order of the requirements applies.
3. A requirement is only done when **code, tests, XML docs, a doc page (DocFX, English) and, if applicable, a sample** exist and all acceptance criteria are met (rule "feature = code + tests + docs + sample" from the specification).
4. Existing rules still apply: nullable enabled, warnings as errors, small public API, dependencies only on `Microsoft.*` packages or the ASP.NET Core shared framework, xUnit v3.
5. API names in this file are **proposals**. If you deviate, explain why in the decision log.
6. At the end of every milestone: output the acceptance criteria as a checklist, update `CHANGELOG.md` and the decision log in `docs/SPEC.md`.
7. For ambiguities that affect the public API: ask, do not guess.

## 3. Changes to previous decisions

| Previously (SPEC.md) | New |
|---|---|
| ASP.NET Core only after v1 ("Other host types not in v1") | ASP.NET Core and Blazor support come in **v1** (milestone M1) |
| Enabling/disabling only in v2 | Enabling/disabling via configuration in **v1** (M2). Restart service and admin storage remain for later. |
| Failure policy only global (fail-fast, optionally "skip and log") | Additionally configurable **per plugin**, plus marking as a required plugin |
| "When skipping, dependent plugins are skipped as well" as a proposal | **Decided** (R-11) |
| Runtime behavior only through normal DI means, no plugin hooks | Additionally **one** asynchronous startup hook with a fixed order (R-31). Rationale: order and "done before the first request" cannot be guaranteed reliably with hosted services alone. |

## 4. New packages

| Package | Content | Dependencies |
|---|---|---|
| `Plugwright.AspNetCore` | Handler for web plugins (controllers, endpoints, middleware), health check | `Plugwright`, shared framework `Microsoft.AspNetCore.App` |
| `Plugwright.Blazor` | Handler for Blazor plugins (component assemblies for the router) | `Plugwright.AspNetCore` |

The plugin contracts of the new packages (`IWebPlugin`, `IBlazorPlugin`) live in `Plugwright.Abstractions` or in their own `*.Abstractions` packages, so plugins do not have to reference the whole main package. Decide based on the existing package split and document the choice.

## 5. Milestones

| Milestone | Requirements | Result |
|---|---|---|
| **M1 Web foundation** | R-01, R-02, R-03, R-04, R-05 | Web app with plugins that bring services, API and Blazor pages |
| **M2 State and failures** | R-10, R-11, R-12, R-13, R-16 | Enabling/disabling, required plugins, catalog with state, catalog as an endpoint for other hosts |
| **M3 Startup and testing** | R-31, R-40, R-41 | Startup hook, testing support, docs for everyday development |
| **M4 Later** | R-14, R-15, R-17, R-32, R-50, R-51, R-52, R-53 | only start after consultation |

---

## M1 Web foundation

### R-01 ASP.NET Core as host

**Description:** `builder.Plugins`, `builder.ApplyPlugins()` and `app.UsePlugins()` work with `WebApplicationBuilder` and `WebApplication`, without a special path for the user.

**Usage:**

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();              // optional, Aspire
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Plugins.Add<OrdersPlugin>();
builder.PluginHandlers.Add<WebPluginHandler>();
builder.PluginHandlers.Add<BlazorPluginHandler>();
builder.ApplyPlugins();                    // Phase 1

var app = builder.Build();
app.UseAntiforgery();
app.UsePlugins();                          // Phase 2: middleware/endpoints of the plugins
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddPluginAssemblies(app);              // from R-04
app.Run();
```

**Rules:**

- `WebApplicationBuilder` implements `IHostApplicationBuilder`, `WebApplication` implements `IHost`. The existing extension members should apply without changes. If they do not, this is fixed, not worked around.
- The position of `UsePlugins()` determines the position of the plugin middleware in the pipeline. This is documented.
- Handlers that need web features check in phase 2 whether `app` is a `WebApplication` (or `IApplicationBuilder` and `IEndpointRouteBuilder`). If not, they throw a clear exception pointing to the host type.

**Acceptance criteria:**

- [ ] Sample `samples/WebHost` starts with a plugin that registers a service used in an endpoint.
- [ ] Integration test with `WebApplicationFactory<Program>` confirms the endpoint.
- [ ] The console samples keep working unchanged.

### R-02 Working with .NET Aspire

**Description:** Plugwright works together with `AddServiceDefaults()` from an Aspire ServiceDefaults project. Plugins can register their own health checks and OpenTelemetry sources.

**Rules:**

- The recommended order is `AddServiceDefaults()` before `ApplyPlugins()`. It is documented, but the reverse order must not break.
- Plugwright logs use their own documented category (e.g. `Plugwright`) so they can be filtered in the Aspire dashboard.
- Plugwright gets an `ActivitySource` or a `Meter` named `Plugwright`, if that is possible without additional dependencies (`System.Diagnostics`). Otherwise note it as a follow-up requirement.

**Acceptance criteria:**

- [ ] How-to page "Using Plugwright with .NET Aspire" with a runnable sample `samples/SplitHosts`:
  - AppHost, ServiceDefaults
  - API host with a backend plugin
  - Blazor web host with a UI plugin that calls the API via service discovery
  - The plugin's Abstractions project, used by both hosts
- [ ] A plugin registers a health check that appears under `/health`.

### R-03 Web plugins: controllers, endpoints, middleware

**Description:** New handler `WebPluginHandler` in the package `Plugwright.AspNetCore`. It makes controllers from plugin assemblies discoverable and lets plugins contribute endpoints and middleware.

**Contract (proposal):**

```csharp
public interface IWebPlugin
{
    // Phase 2: endpoints (minimal APIs, hubs …)
    void MapEndpoints(IEndpointRouteBuilder endpoints) { }

    // Phase 2: middleware at the position of app.UsePlugins()
    void ConfigurePipeline(IApplicationBuilder app) { }
}
```

**Rules:**

- Phase 1: For every assembly of a plugin that implements `IWebPlugin`, an `ApplicationPart` is registered so that MVC controllers (including OData controllers) are found. The handler must not force `AddControllers()` itself. If the host does not call `AddControllers()`, the parts are provided anyway (via `ApplicationPartManager`, as soon as MVC is registered).
- Plugins without controllers may still implement `IWebPlugin` (endpoints or middleware only).
- Phase 2: first `ConfigurePipeline` of all plugins, then `MapEndpoints`, each in dependency order.
- Plugin instance: the same instance as in phase 1 (default handler). If that is not possible, document how state is shared between the phases.

**Acceptance criteria:**

- [ ] A controller in a plugin assembly is reachable without the host knowing the assembly.
- [ ] A minimal API endpoint from `MapEndpoints` is reachable.
- [ ] Middleware from `ConfigurePipeline` runs at the position of `UsePlugins()` (test with an order header).
- [ ] Integration tests for all three cases.

### R-04 Blazor plugins: pages and components

**Description:** New handler `BlazorPluginHandler` in the package `Plugwright.Blazor`. It collects the component assemblies of the plugins so that the router and `MapRazorComponents` find plugin pages.

**Contract (proposal):**

```csharp
public interface IBlazorPlugin { }   // Marker; the plugin's assemblies contain Razor components

public interface IPluginComponentAssemblies
{
    IReadOnlyList<Assembly> Assemblies { get; }
}
```

**Rules:**

- Phase 1: The handler registers `IPluginComponentAssemblies` as a singleton with all assemblies of plugins that implement `IBlazorPlugin`, in dependency order and without duplicates.
- The extension `AddPluginAssemblies(this RazorComponentsEndpointConventionBuilder, IHost app)` calls `AddAdditionalAssemblies(...)` with these assemblies.
- For the `<Router>`, the sample documents how `AdditionalAssemblies` is filled via `@inject IPluginComponentAssemblies`.
- Navigation entries, menus or page sections are **app-specific contracts**. Plugwright provides no types of its own for them. The sample shows how a plugin registers such app-owned services via DI.
- Static web assets (`_content/<Assembly>/…`) work automatically with a project reference. The sample demonstrates this. Dynamically loaded plugins: see R-51.

**Acceptance criteria:**

- [ ] Sample `samples/BlazorHost` (Blazor Web App, Interactive Server, **without** its own API and without a database) with a plugin as a Razor class library that brings a page `/orders` and a CSS file.
- [ ] The Blazor handler has no runtime dependency on MVC or the web plugin handler. A web host without `AddControllers()` works.
- [ ] The page is reachable via navigation and via direct access, the CSS file is loaded.
- [ ] Test (bUnit or integration test) that the plugin route is resolved.

### R-05 Several plugin kinds and plugins across several hosts

**Description:**

- A plugin can be `IPlugin`, `IWebPlugin` and `IBlazorPlugin` at the same time. All responsible handlers process it (existing rule "all matching handlers run").
- More common is the split setup: backend part (`IPlugin` + `IWebPlugin`) in the API host, UI part (`IPlugin` + `IBlazorPlugin`) in the web host, an Abstractions project with the DTOs in between.
- Both setups are covered by tests and documented.

**Rules:**

- Backend and UI parts are separate plugins with their own ID. Recommended convention: `<id>` and `<id>.ui`. Plugwright does not enforce the convention.
- Abstractions assemblies of the app or a plugin are not plugins. They are referenced normally.

**Acceptance criteria:**

- [ ] A test plugin implements all three contracts. Services, controller and page work simultaneously in one host.
- [ ] `samples/SplitHosts` (from R-02) shows the split setup.
- [ ] Doc pages "One plugin, several kinds" and "Splitting a plugin across an API host and a UI host".

---

## M2 State and failures

### R-10 Enabling and disabling via configuration

**Description:** Every plugin can be disabled via `Plugwright:Plugins:{Id}:Enabled` (default `true`). `ApplyPlugins()` does not load disabled plugins. The change takes effect on the next start.

```json
{
  "Plugwright": {
    "Plugins": {
      "Acme.Reports": { "Enabled": false }
    }
  }
}
```

**Rules:**

- The value is read from the merged configuration (appsettings.json, pluginsettings.json, environment variables) according to the existing rule of the specification.
- Disabled means: no handler runs for the plugin, no services are registered, no assemblies are made known to the router or MVC.
- The plugin's metadata is still read so that it appears in the catalog (R-12).
- Plugins that depend on a disabled plugin are handled according to R-11.
- A required plugin (R-13) cannot be disabled. `Enabled: false` for a required plugin leads to an exception with a clear message at startup.

**Acceptance criteria:**

- [ ] Test: a plugin with `Enabled: false` registers no services and appears in the catalog as `Disabled`.
- [ ] Test: an environment variable overrides the value from appsettings.json (or the order according to the specification).
- [ ] Doc page "Enabling and disabling plugins".

### R-11 Skip dependent plugins on failure

**Description:** If a plugin is not loaded (disabled, skipped after an error, verification failed, dependency missing), all plugins that depend on it directly or indirectly are not loaded either.

**Rules:**

- Reason in the catalog: `DependencyUnavailable`, with the ID of the triggering dependency and its state.
- If a dependent plugin is required, an exception names the whole chain (e.g. "Required plugin B cannot start because dependency A is disabled").
- The resolution happens before phase 1, i.e. before any plugin runs `Configure`. Exception: errors in `Configure` itself (see R-13); then dependent plugins whose `Configure` has not run yet are skipped.

**Acceptance criteria:**

- [ ] Test: A disabled, B depends on A, C on B → B and C appear as `DependencyUnavailable`.
- [ ] Test: B is required → the exception names A and B.

### R-12 Plugin catalog

**Description:** A service `IPluginCatalog` (singleton in the DI container) provides **all known** plugins with metadata and state, including those not loaded.

**Contract (proposal):**

```csharp
public interface IPluginCatalog
{
    IReadOnlyList<PluginInfo> Plugins { get; }
    PluginInfo? Find(string id);
}

public sealed record PluginInfo(
    string Id,
    string Version,
    string? Description,
    PluginSource Source,                 // Direct, Directory, Package
    PluginState State,
    string? Reason,                      // human-readable
    string? ErrorMessage,                // exception message, without stack trace
    bool IsRequired,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> AppliedHandlers);

public enum PluginState
{
    Loaded,
    Disabled,
    DependencyUnavailable,
    Failed,                // error in Configure or in the startup hook (R-31)
    VerificationFailed,
    NoHandler
}
```

**Rules:**

- The catalog replaces or extends the existing "plugin list as a singleton". If the existing list is already public, it stays compatible or is marked obsolete.
- Results of the integrity verification are reachable or linked via the catalog (reuse the existing API).
- State changes after startup (R-31: error in the startup hook) are recorded in the catalog. The catalog is thread-safe.
- A structured log event with a fixed EventId per plugin at startup (ID, version, state, reason).

**Acceptance criteria:**

- [ ] Tests for every `PluginState`.
- [ ] Sample page in `samples/BlazorHost` that shows the catalog as a table.
- [ ] Doc page "Inspecting plugins at runtime".

### R-13 Failure policy per plugin and required plugins

**Description:** In addition to the global failure policy, the host can decide per plugin whether it is **required** (fail-fast) or **optional** (skip and log).

**Usage (proposal):**

```csharp
builder.Plugins.Options.DefaultFailurePolicy = PluginFailurePolicy.FailFast;   // existing, default

builder.Plugins.Add<OrdersPlugin>(o => o.IsRequired = true);
builder.Plugins.Add<ReportsPlugin>(o => o.FailurePolicy = PluginFailurePolicy.SkipAndLog);
builder.Plugins.Require("Acme.Billing");      // for plugins from a directory/package
```

**Rules:**

- Required is a property that **only code** sets, never configuration. A writable file must not be able to turn off required plugins (same principle as for the verifier settings).
- A required plugin is missing, disabled, has an unavailable dependency, fails verification or throws in `Configure` → exception in `ApplyPlugins()`. The message names **all** required plugins with problems at once, not only the first.
- An optional plugin throws in `Configure` → state `Failed`, dependent plugins according to R-11, startup continues. Services this plugin already registered are removed again if possible. If that is not reliably possible, this is documented and the plugin is additionally logged as "partially registered".
- `Require(id)` for an ID that no source provides → exception "Required plugin not found".

**Acceptance criteria:**

- [ ] Tests: required missing, required disabled, required throws, optional throws (app starts, catalog `Failed`), several required failures in one message.
- [ ] Test: `Enabled: false` in pluginsettings.json for a required plugin → exception.
- [ ] Doc page "Required and optional plugins".

### R-16 Plugin catalog as an endpoint

**Description:** The API host can expose its plugin catalog (R-12) via a read-only HTTP endpoint. This way another host, e.g. the web host, learns which backend plugins are active and can hide the corresponding UI plugins.

**Usage (proposal, package `Plugwright.AspNetCore`):**

```csharp
app.MapPluginCatalog("/_plugwright/plugins")
   .RequireAuthorization();          // normal endpoint builder
```

**Rules:**

- Response: JSON list with ID, version, state and reason. Error messages and stack traces are not returned. Whether `ErrorMessage` is included is configurable via an option, default `false`.
- The return type lives as a DTO in an Abstractions package, so a client can deserialize it without the main package.
- No writing via the endpoint (no enabling/disabling).
- The endpoint is not registered automatically and is not public automatically. The docs point out authorization.

**Acceptance criteria:**

- [ ] Integration test: the endpoint returns all plugins with their state, without error messages by default.
- [ ] `samples/SplitHosts`: the web host reads the API's catalog at startup and hides a UI plugin whose backend part is disabled (solved in the sample code, not in Plugwright; see R-17).

---

## M3 Startup and testing

### R-31 Asynchronous startup hook

**Description:** Plugins can register asynchronous startup tasks that run after `Build()`, in dependency order and **before** all other hosted services (including the web server).

**Contract (proposal):**

```csharp
public interface IPluginStartup
{
    Task StartAsync(IServiceProvider services, CancellationToken cancellationToken);
}

// in Configure(PluginContext context)
context.AddStartup<MigrateDataStartup>();   // type is resolved from DI (one scope per task)
```

**Rules:**

- Implemented via **one** internal service with `IHostedLifecycleService.StartingAsync`, so that all startup tasks run before any `StartAsync` of other hosted services. Verify with a test that the web server does not accept requests before.
- Order: dependency order of the plugins, within a plugin registration order. Strictly sequential, not parallel.
- Every task runs in its own DI scope.
- Errors: required plugin → exception, the host does not start. Optional plugin → catalog `Failed`, startup tasks of dependent plugins are skipped (their state also `DependencyUnavailable`). Their services stay registered because the container is immutable. This is documented; plugins can check their state via `IPluginCatalog`.
- Cancellation via the host's `CancellationToken`. The duration of every task is logged.
- Explicitly **no** stop hook. Cleanup runs via normal hosted services or `IAsyncDisposable`.

**Acceptance criteria:**

- [ ] Test: startup tasks of A and B (depends on A) run in the order A, B.
- [ ] Test: the web server answers no request before all startup tasks are done.
- [ ] Test: error in an optional plugin → app runs, catalog `Failed`, dependent task skipped.
- [ ] Test: error in a required plugin → host start fails.
- [ ] Doc page "Startup tasks", with the distinction from `IHostedService`.

### R-40 Testing support for web hosts

**Description:** Hosts with Plugwright can be tested with `WebApplicationFactory`, including deliberately disabled or failing plugins.

**Rules:**

- Disabling in tests via normal configuration (`ConfigureAppConfiguration` or `UseSetting("Plugwright:Plugins:{Id}:Enabled", "false")`). No separate test API is needed for this, but it must work and be documented.
- For failure scenarios, `tests/TestPlugins` provides a plugin that throws in `Configure` or in the startup hook depending on a setting. It is shown as a pattern in the docs.
- Check whether a small package `Plugwright.Testing` is needed. Only create it if there is real added value, and record the decision in the decision log.

**Acceptance criteria:**

- [ ] Integration tests in `Plugwright.IntegrationTests` use `WebApplicationFactory` for the scenarios from R-10 to R-13 and R-31.
- [ ] Doc page "Testing apps that use Plugwright".

### R-41 Docs for everyday development

**Description:** Guide for plugins via project reference in one solution.

**Content:**

- Solution layout with separate API and web hosts: backend parts as class libraries, UI parts as Razor class libraries, shared Abstractions projects (DTOs) in between.
- Debugging with breakpoints in plugins, `dotnet watch` and hot reload for Razor components in plugins.
- How a plugin provides app-owned extension points (example: menu entries via an app-owned `INavigationItem`).
- Plugwright as a Git submodule with a project reference instead of a NuGet package during joint development.

**Acceptance criteria:**

- [ ] How-to pages exist, code samples included from `samples/` and built in CI.

---

## M4 Later (only after consultation)

Decided on 2026-10-11, details in the specification's decision log. Implemented on 2026-10-11: R-52, R-53, R-50, R-15, R-32 and R-51 (after a spike, `spike-r51-static-assets.md`); R-14 and R-17 as guides. The table shows the original request.

| ID | Requirement | Summary |
|---|---|---|
| R-14 | Replaceable source for the activation state | `IPluginActivationSource`, evaluated in `ApplyPlugins()`, the default is the configuration from R-10. This lets the host read the state e.g. from its own administration. Required plugins stay unaffected. |
| R-17 | Counterpart in another host | A plugin declares that it needs a counterpart in another host (e.g. UI part → backend ID). The host checks this at startup via R-16 and treats a missing counterpart like an unavailable dependency (R-11). Open question: behavior when the other host is unreachable at startup. |
| R-15 | Optional dependencies | already planned for v2; the plugin loads even without the dependency and learns via the catalog whether it is present |
| R-32 | Trigger a restart, also under IIS | extend the existing v2 plan (restart service) with IIS: `StopApplication()` ends the process, IIS restarts on the next request; document the behavior for in-process and out-of-process |
| R-50 | Health check "Plugins" | `AddPlugwrightHealthCheck()`: `Healthy` if all plugins are loaded; `Degraded` if optional ones are missing or failed; data per plugin in the result |
| R-51 | Static web assets of dynamically loaded plugins | Serve `_content/…` from plugin directories or packages, including integrity verification of the files |
| R-52 | Shared assemblies defined by the host | `builder.Plugins.ShareAssembly(typeof(X).Assembly)` or prefixes; these are taken from the default context during dynamic loading (e.g. app abstractions, UI libraries, EF Core) |
| R-53 | Assembly scan | already on the roadmap; find all plugins from referenced assemblies |

## 6. Out of scope

- Hot reload or unloading of plugins at runtime (stays excluded).
- App-specific extension points such as menus, data types or permissions. The host application defines them itself; Plugwright only transports them via DI.
- Data storage of plugins (database schemas, migrations). Plugwright only provides the startup hook (R-31).
- Blazor WebAssembly. Only Blazor Server or Interactive Server is the target.

## 7. Open points to decide during implementation

1. Do `IWebPlugin` and `IBlazorPlugin` live in `Plugwright.Abstractions` or in their own Abstractions packages?
2. Are plugin instances shared between phase 1 and phase 2 (R-03)? So far the default handler instantiates in phase 1.
3. Can services of an optional plugin that failed in `Configure` be taken back reliably (R-13), e.g. via an intermediate `ServiceCollection`?
4. EventIds and category names for the new log events.

## 8. Start prompt for Claude Code

```text
Read CLAUDE.md, docs/SPEC.md and docs/requirements/REQUIREMENTS-web-host.md completely.
Create an implementation plan for milestone M1 (R-01 to R-05): affected projects,
new packages, public API with signatures, tests, samples and doc pages.
Answer open points 1 and 2 from section 7 with a recommendation.
Wait for my approval before you write code.
```
