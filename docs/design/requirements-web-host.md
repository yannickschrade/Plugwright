# Plugwright – Anforderungen: Web-Hosts, Plugin-Zustand und Startablauf

Stand: 10.10.2026 · Status: freigegeben zur Umsetzung

Diese Datei erweitert die bestehende Spezifikation (`docs/SPEC.md`, Stand 06.10.2026). Sie beschreibt, was Plugwright zusätzlich können muss, damit eine **ASP.NET-Core-/Blazor-Server-Anwendung** mit fachlichen Plugins darauf aufbauen kann. Wo diese Datei der Spezifikation widerspricht, gilt diese Datei. Die Abweichungen sind unten aufgelistet und werden bei der Umsetzung ins Entscheidungslog der Spezifikation übernommen.

## 1. Ausgangslage

Eine typische Zielanwendung:

- **Zwei getrennte Hosts**, beide mit Plugwright:
  - **API-Host** (ASP.NET Core): REST/OData-API, Fachlogik, Datenbank, Hintergrunddienste.
  - **Web-Host** (Blazor, Interactive Server): Oberfläche, ruft die API per HTTP auf und hat keinen Datenbankzugriff.
  - Optional mit .NET Aspire (`AddServiceDefaults()`), das beide Hosts startet.
- Fachliche Funktionen kommen als Plugins. Ein fachliches Plugin besteht meist aus **drei Projekten**:
  - **Backend-Teil** (Dienste, API-Controller), geladen im API-Host
  - **Abstractions** (DTOs, Schnittstellen), von beiden genutzt
  - **UI-Teil** (Razor Class Library), geladen im Web-Host

  Backend- und UI-Teil sind für Plugwright zwei eigene Plugins in zwei Prozessen.
- Der Web-Host muss wissen, welche Backend-Plugins in der API aktiv sind, um zugehörige UI-Plugins auszublenden.
- Plugins sind per **Projektreferenz** eingebunden (`Plugins.Add<T>()`). Dynamisches Laden aus Verzeichnissen kommt später.
- Manche Plugins sind **Pflicht** (die App darf ohne sie nicht starten), andere **optional** (die App startet ohne sie und zeigt den Zustand an).
- Plugins müssen beim Start **asynchrone Vorbereitungen** treffen können, z. B. Datenbankschemata registrieren oder Daten migrieren, bevor die App Anfragen annimmt.
- Plugins werden über die Konfiguration **aktiviert und deaktiviert**. Die Änderung wirkt nach einem Neustart.

## 2. Arbeitsanweisung für Claude Code

1. Lies `docs/SPEC.md` und diese Datei vollständig, bevor du anfängst.
2. Setze die Meilensteine **in der angegebenen Reihenfolge** um. Innerhalb eines Meilensteins gilt die Reihenfolge der Anforderungen.
3. Jede Anforderung ist erst fertig, wenn **Code, Tests, XML-Doku, Doku-Seite (DocFX, Englisch) und ggf. Sample** vorliegen und alle Akzeptanzkriterien erfüllt sind (Regel „Funktion = Code + Tests + Doku + Beispiel“ aus der Spezifikation).
4. Bestehende Regeln gelten weiter: Nullable aktiv, Warnungen als Fehler, öffentliche API klein, nur Abhängigkeiten auf `Microsoft.*`-Pakete bzw. das ASP.NET-Core-Shared-Framework, xUnit v3.
5. API-Namen in dieser Datei sind **Vorschläge**. Weichst du ab, begründe es im Entscheidungslog.
6. Am Ende jedes Meilensteins: Akzeptanzkriterien als Checkliste ausgeben, `CHANGELOG.md` und Entscheidungslog in `docs/SPEC.md` aktualisieren.
7. Bei Unklarheiten, die das öffentliche API betreffen: nachfragen, nicht raten.

## 3. Änderungen an bisherigen Festlegungen

| Bisher (SPEC.md) | Neu |
|---|---|
| ASP.NET Core erst nach v1 („Andere Host-Typen nicht in v1“) | ASP.NET-Core- und Blazor-Unterstützung kommen in **v1** (Meilenstein M1) |
| Aktivieren/Deaktivieren erst in v2 | Aktivieren/Deaktivieren über Konfiguration in **v1** (M2). Neustart-Dienst und Admin-Speicherung bleiben später. |
| Fehler-Policy nur global (Fail-fast, optional „überspringen und loggen“) | Zusätzlich **je Plugin** einstellbar, plus Kennzeichnung als Pflicht-Plugin |
| „Bei Überspringen werden abhängige Plugins mit übersprungen“ als Vorschlag | **Entschieden** (R-11) |
| Laufzeitverhalten nur über normale DI-Mittel, keine Plugin-Hooks | Zusätzlich **ein** asynchroner Start-Hook mit fester Reihenfolge (R-31). Begründung: Reihenfolge und „fertig vor dem ersten Request“ lassen sich mit Hosted Services allein nicht zuverlässig garantieren. |

## 4. Neue Pakete

| Paket | Inhalt | Abhängigkeiten |
|---|---|---|
| `Plugwright.AspNetCore` | Handler für Web-Plugins (Controller, Endpunkte, Middleware), Health Check | `Plugwright`, Shared Framework `Microsoft.AspNetCore.App` |
| `Plugwright.Blazor` | Handler für Blazor-Plugins (Komponenten-Assemblies für den Router) | `Plugwright.AspNetCore` |

Die Plugin-Verträge der neuen Pakete (`IWebPlugin`, `IBlazorPlugin`) liegen in `Plugwright.Abstractions` oder in eigenen `*.Abstractions`-Paketen, damit Plugins nicht das ganze Hauptpaket referenzieren müssen. Entscheide nach dem bestehenden Paketschnitt und dokumentiere die Wahl.

## 5. Meilensteine

| Meilenstein | Anforderungen | Ergebnis |
|---|---|---|
| **M1 Web-Grundlage** | R-01, R-02, R-03, R-04, R-05 | Web-App mit Plugins, die Dienste, API und Blazor-Seiten mitbringen |
| **M2 Zustand und Fehler** | R-10, R-11, R-12, R-13, R-16 | Aktivieren/Deaktivieren, Pflicht-Plugins, Katalog mit Zustand, Katalog als Endpunkt für andere Hosts |
| **M3 Start und Test** | R-31, R-40, R-41 | Start-Hook, Testunterstützung, Doku für den Entwicklungsalltag |
| **M4 später** | R-14, R-15, R-17, R-32, R-50, R-51, R-52, R-53 | erst nach Rücksprache beginnen |

---

## M1 Web-Grundlage

### R-01 ASP.NET Core als Host

**Beschreibung:** `builder.Plugins`, `builder.ApplyPlugins()` und `app.UsePlugins()` funktionieren mit `WebApplicationBuilder` bzw. `WebApplication`, ohne Sonderweg für den Nutzer.

**Nutzung:**

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
app.UsePlugins();                          // Phase 2: Middleware/Endpunkte der Plugins
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddPluginAssemblies(app);              // aus R-04
app.Run();
```

**Regeln:**

- `WebApplicationBuilder` implementiert `IHostApplicationBuilder`, `WebApplication` implementiert `IHost`. Die bestehenden Extension Members sollen ohne Änderung greifen. Ist das nicht der Fall, wird es behoben, nicht umgangen.
- Die Position von `UsePlugins()` bestimmt die Position der Plugin-Middleware in der Pipeline. Das wird dokumentiert.
- Handler, die Web-Funktionen brauchen, prüfen in Phase 2, ob `app` eine `WebApplication` (bzw. `IApplicationBuilder` und `IEndpointRouteBuilder`) ist. Wenn nicht, gibt es eine verständliche Exception mit Hinweis auf den Host-Typ.

**Akzeptanzkriterien:**

- [ ] Sample `samples/WebHost` startet mit einem Plugin, das einen Dienst registriert, der in einem Endpunkt genutzt wird.
- [ ] Integrationstest mit `WebApplicationFactory<Program>` bestätigt den Endpunkt.
- [ ] Die Konsolen-Samples laufen unverändert weiter.

### R-02 Zusammenspiel mit .NET Aspire

**Beschreibung:** Plugwright funktioniert zusammen mit `AddServiceDefaults()` aus einem Aspire-ServiceDefaults-Projekt. Plugins können eigene Health Checks und OpenTelemetry-Quellen registrieren.

**Regeln:**

- Die empfohlene Reihenfolge ist `AddServiceDefaults()` vor `ApplyPlugins()`. Sie wird dokumentiert, die umgekehrte Reihenfolge darf aber nicht brechen.
- Plugwright-Logs nutzen eine eigene, dokumentierte Kategorie (z. B. `Plugwright`), damit sie im Aspire-Dashboard filterbar sind.
- Plugwright bekommt eine `ActivitySource` bzw. einen `Meter` mit dem Namen `Plugwright`, sofern das ohne zusätzliche Abhängigkeiten möglich ist (`System.Diagnostics`). Sonst als Folgeanforderung notieren.

**Akzeptanzkriterien:**

- [ ] How-to-Seite „Using Plugwright with .NET Aspire“ mit lauffähigem Sample `samples/SplitHosts`:
  - AppHost, ServiceDefaults
  - API-Host mit Backend-Plugin
  - Blazor-Web-Host mit UI-Plugin, der die API über Service Discovery aufruft
  - Abstractions-Projekt des Plugins, von beiden Hosts genutzt
- [ ] Ein Plugin registriert einen Health Check, der unter `/health` erscheint.

### R-03 Web-Plugins: Controller, Endpunkte, Middleware

**Beschreibung:** Neuer Handler `WebPluginHandler` im Paket `Plugwright.AspNetCore`. Er macht Controller aus Plugin-Assemblies auffindbar und lässt Plugins Endpunkte und Middleware beitragen.

**Vertrag (Vorschlag):**

```csharp
public interface IWebPlugin
{
    // Phase 2: Endpunkte (Minimal APIs, Hubs …)
    void MapEndpoints(IEndpointRouteBuilder endpoints) { }

    // Phase 2: Middleware an der Stelle von app.UsePlugins()
    void ConfigurePipeline(IApplicationBuilder app) { }
}
```

**Regeln:**

- Phase 1: Für jede Assembly eines Plugins, das `IWebPlugin` implementiert, wird ein `ApplicationPart` registriert, damit MVC-Controller (auch OData-Controller) gefunden werden. Der Handler darf `AddControllers()` nicht selbst erzwingen. Ruft der Host `AddControllers()` nicht auf, werden die Parts trotzdem bereitgestellt (über `ApplicationPartManager`, sobald MVC registriert ist).
- Plugins ohne Controller dürfen `IWebPlugin` trotzdem implementieren (nur Endpunkte bzw. Middleware).
- Phase 2: zuerst `ConfigurePipeline` aller Plugins, dann `MapEndpoints`, jeweils in Abhängigkeitsreihenfolge.
- Plugin-Instanz: dieselbe Instanz wie in Phase 1 (Standard-Handler). Ist das nicht möglich, dokumentieren, wie Zustand zwischen den Phasen geteilt wird.

**Akzeptanzkriterien:**

- [ ] Ein Controller in einer Plugin-Assembly ist erreichbar, ohne dass der Host die Assembly kennt.
- [ ] Ein Minimal-API-Endpunkt aus `MapEndpoints` ist erreichbar.
- [ ] Middleware aus `ConfigurePipeline` läuft an der Position von `UsePlugins()` (Test mit Reihenfolge-Header).
- [ ] Integrationstests für alle drei Fälle.

### R-04 Blazor-Plugins: Seiten und Komponenten

**Beschreibung:** Neuer Handler `BlazorPluginHandler` im Paket `Plugwright.Blazor`. Er sammelt die Komponenten-Assemblies der Plugins, damit Router und `MapRazorComponents` Plugin-Seiten finden.

**Vertrag (Vorschlag):**

```csharp
public interface IBlazorPlugin { }   // Marker; Assemblies des Plugins enthalten Razor-Komponenten

public interface IPluginComponentAssemblies
{
    IReadOnlyList<Assembly> Assemblies { get; }
}
```

**Regeln:**

- Phase 1: Der Handler registriert `IPluginComponentAssemblies` als Singleton mit allen Assemblies von Plugins, die `IBlazorPlugin` implementieren, in Abhängigkeitsreihenfolge und ohne Duplikate.
- Erweiterung `AddPluginAssemblies(this RazorComponentsEndpointConventionBuilder, IHost app)` ruft `AddAdditionalAssemblies(...)` mit diesen Assemblies auf.
- Für den `<Router>` dokumentiert das Sample, wie `AdditionalAssemblies` per `@inject IPluginComponentAssemblies` befüllt wird.
- Navigationseinträge, Menüs oder Seitenabschnitte sind **app-spezifische Verträge**. Plugwright liefert dafür keine eigenen Typen. Das Sample zeigt, wie ein Plugin solche app-eigenen Dienste über DI registriert.
- Statische Web-Assets (`_content/<Assembly>/…`) funktionieren bei Projektreferenz automatisch. Das wird im Sample nachgewiesen. Dynamisch geladene Plugins: siehe R-51.

**Akzeptanzkriterien:**

- [ ] Sample `samples/BlazorHost` (Blazor Web App, Interactive Server, **ohne** eigene API und ohne Datenbank) mit einem Plugin als Razor Class Library, das eine Seite `/orders` und eine CSS-Datei mitbringt.
- [ ] Der Blazor-Handler hat keine Abhängigkeit auf MVC bzw. den Web-Plugin-Handler zur Laufzeit. Ein Web-Host ohne `AddControllers()` funktioniert.
- [ ] Die Seite ist per Navigation und per Direktaufruf erreichbar, die CSS-Datei wird geladen.
- [ ] Test (bUnit oder Integrationstest), dass die Plugin-Route aufgelöst wird.

### R-05 Mehrere Plugin-Arten und Plugins über mehrere Hosts

**Beschreibung:**

- Ein Plugin kann gleichzeitig `IPlugin`, `IWebPlugin` und `IBlazorPlugin` sein. Alle zuständigen Handler verarbeiten es (bestehende Regel „alle passenden Handler laufen“).
- Häufiger ist der getrennte Aufbau: Backend-Teil (`IPlugin` + `IWebPlugin`) im API-Host, UI-Teil (`IPlugin` + `IBlazorPlugin`) im Web-Host, ein Abstractions-Projekt mit den DTOs dazwischen.
- Beide Aufbauten werden abgesichert und dokumentiert.

**Regeln:**

- Backend- und UI-Teil sind eigene Plugins mit eigener Id. Empfohlene Konvention: `<id>` und `<id>.ui`. Plugwright erzwingt die Konvention nicht.
- Abstractions-Assemblies der App bzw. eines Plugins sind keine Plugins. Sie werden normal referenziert.

**Akzeptanzkriterien:**

- [ ] Ein Test-Plugin implementiert alle drei Verträge. Dienste, Controller und Seite funktionieren gleichzeitig in einem Host.
- [ ] `samples/SplitHosts` (aus R-02) zeigt den getrennten Aufbau.
- [ ] Doku-Seiten „One plugin, several kinds“ und „Splitting a plugin across an API host and a UI host“.

---

## M2 Zustand und Fehler

### R-10 Aktivieren und Deaktivieren über Konfiguration

**Beschreibung:** Jedes Plugin lässt sich über `Plugwright:Plugins:{Id}:Enabled` (Standard `true`) deaktivieren. `ApplyPlugins()` lädt deaktivierte Plugins nicht. Die Änderung wirkt beim nächsten Start.

```json
{
  "Plugwright": {
    "Plugins": {
      "Acme.Reports": { "Enabled": false }
    }
  }
}
```

**Regeln:**

- Gelesen wird aus der zusammengeführten Konfiguration (appsettings.json, pluginsettings.json, Umgebungsvariablen) nach der bestehenden Regel der Spezifikation.
- Deaktiviert bedeutet: Kein Handler läuft für das Plugin, keine Dienste werden registriert, keine Assemblies werden dem Router oder MVC bekannt gemacht.
- Die Metadaten des Plugins werden trotzdem gelesen, damit es im Katalog (R-12) erscheint.
- Plugins, die von einem deaktivierten Plugin abhängen, werden nach R-11 behandelt.
- Ein Pflicht-Plugin (R-13) kann nicht deaktiviert werden. `Enabled: false` bei einem Pflicht-Plugin führt beim Start zu einer Exception mit klarer Meldung.

**Akzeptanzkriterien:**

- [ ] Test: Plugin mit `Enabled: false` registriert keine Dienste und erscheint im Katalog als `Disabled`.
- [ ] Test: Umgebungsvariable überschreibt den Wert aus appsettings.json (bzw. Reihenfolge nach Spezifikation).
- [ ] Doku-Seite „Enabling and disabling plugins“.

### R-11 Abhängige Plugins bei Ausfall mit überspringen

**Beschreibung:** Wird ein Plugin nicht geladen (deaktiviert, übersprungen nach Fehler, Prüfung fehlgeschlagen, Abhängigkeit fehlt), werden alle Plugins, die direkt oder indirekt davon abhängen, ebenfalls nicht geladen.

**Regeln:**

- Grund im Katalog: `DependencyUnavailable`, mit Id der auslösenden Abhängigkeit und deren Zustand.
- Ist ein abhängiges Plugin Pflicht, gibt es eine Exception, die die ganze Kette nennt (z. B. „Required plugin B cannot start because dependency A is disabled“).
- Die Auflösung erfolgt vor Phase 1, also bevor irgendein Plugin `Configure` ausführt. Ausnahme: Fehler in `Configure` selbst (siehe R-13); dann werden abhängige Plugins, deren `Configure` noch nicht lief, übersprungen.

**Akzeptanzkriterien:**

- [ ] Test: A deaktiviert, B hängt von A ab, C von B → B und C erscheinen als `DependencyUnavailable`.
- [ ] Test: B ist Pflicht → Exception nennt A und B.

### R-12 Plugin-Katalog

**Beschreibung:** Ein Dienst `IPluginCatalog` (Singleton im DI-Container) liefert **alle bekannten** Plugins mit Metadaten und Zustand, auch nicht geladene.

**Vertrag (Vorschlag):**

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
    string? Reason,                      // für Menschen lesbar
    string? ErrorMessage,                // Exception-Nachricht, ohne Stacktrace
    bool IsRequired,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> AppliedHandlers);

public enum PluginState
{
    Loaded,
    Disabled,
    DependencyUnavailable,
    Failed,                // Fehler in Configure oder im Start-Hook (R-31)
    VerificationFailed,
    NoHandler
}
```

**Regeln:**

- Der Katalog ersetzt bzw. erweitert die bestehende „Plugin-Liste als Singleton“. Ist die bestehende Liste schon öffentlich, bleibt sie kompatibel oder wird als veraltet markiert.
- Prüfergebnisse der Integritätsprüfung sind über den Katalog erreichbar oder verlinkt (bestehende API wiederverwenden).
- Zustandsänderungen nach dem Start (R-31: Fehler im Start-Hook) werden im Katalog nachgetragen. Der Katalog ist threadsicher.
- Ein strukturiertes Log-Event mit fester Event-ID je Plugin beim Start (Id, Version, Zustand, Grund).

**Akzeptanzkriterien:**

- [ ] Tests für jeden `PluginState`.
- [ ] Sample-Seite im `samples/BlazorHost`, die den Katalog als Tabelle zeigt.
- [ ] Doku-Seite „Inspecting plugins at runtime“.

### R-13 Fehler-Policy je Plugin und Pflicht-Plugins

**Beschreibung:** Zusätzlich zur globalen Fehler-Policy kann der Host je Plugin festlegen, ob es **Pflicht** ist (Fail-fast) oder **optional** (überspringen und loggen).

**Nutzung (Vorschlag):**

```csharp
builder.Plugins.Options.DefaultFailurePolicy = PluginFailurePolicy.FailFast;   // bestehend, Standard

builder.Plugins.Add<OrdersPlugin>(o => o.IsRequired = true);
builder.Plugins.Add<ReportsPlugin>(o => o.FailurePolicy = PluginFailurePolicy.SkipAndLog);
builder.Plugins.Require("Acme.Billing");      // für Plugins aus Verzeichnis/Paket
```

**Regeln:**

- Pflicht ist eine Eigenschaft, die **nur der Code** setzt, nie die Konfiguration. Eine beschreibbare Datei darf Pflicht-Plugins nicht abschalten können (gleiches Prinzip wie bei den Prüfer-Einstellungen).
- Pflicht-Plugin fehlt, ist deaktiviert, hat eine nicht verfügbare Abhängigkeit, besteht die Prüfung nicht oder wirft in `Configure` → Exception beim `ApplyPlugins()`. Die Meldung nennt **alle** Pflicht-Plugins mit Problemen auf einmal, nicht nur das erste.
- Optionales Plugin wirft in `Configure` → Zustand `Failed`, abhängige Plugins nach R-11, Start läuft weiter. Bereits von diesem Plugin registrierte Dienste werden nach Möglichkeit wieder entfernt. Geht das nicht zuverlässig, wird das dokumentiert und das Plugin wird zusätzlich als „teilweise registriert“ geloggt.
- `Require(id)` für eine Id, die keine Quelle liefert → Exception „Required plugin not found“.

**Akzeptanzkriterien:**

- [ ] Tests: Pflicht fehlt, Pflicht deaktiviert, Pflicht wirft, optional wirft (App startet, Katalog `Failed`), mehrere Pflicht-Fehler in einer Meldung.
- [ ] Test: `Enabled: false` in pluginsettings.json für ein Pflicht-Plugin → Exception.
- [ ] Doku-Seite „Required and optional plugins“.

### R-16 Plugin-Katalog als Endpunkt

**Beschreibung:** Der API-Host kann seinen Plugin-Katalog (R-12) über einen lesenden HTTP-Endpunkt bereitstellen. So erfährt ein anderer Host, z. B. der Web-Host, welche Backend-Plugins aktiv sind, und kann zugehörige UI-Plugins ausblenden.

**Nutzung (Vorschlag, Paket `Plugwright.AspNetCore`):**

```csharp
app.MapPluginCatalog("/_plugwright/plugins")
   .RequireAuthorization();          // normaler Endpoint-Builder
```

**Regeln:**

- Antwort: JSON-Liste mit Id, Version, Zustand und Grund. Fehlermeldungen und Stacktraces werden nicht ausgegeben. Ob `ErrorMessage` enthalten ist, ist per Option einstellbar, Standard `false`.
- Der Rückgabetyp liegt als DTO in einem Abstractions-Paket, damit ein Client ihn ohne das Hauptpaket deserialisieren kann.
- Kein Schreiben über den Endpunkt (kein Aktivieren/Deaktivieren).
- Der Endpunkt ist nicht automatisch registriert und nicht automatisch öffentlich. Die Doku weist auf Autorisierung hin.

**Akzeptanzkriterien:**

- [ ] Integrationstest: Endpunkt liefert alle Plugins mit Zustand, ohne Fehlermeldungen im Standard.
- [ ] `samples/SplitHosts`: Der Web-Host liest den Katalog der API beim Start und blendet ein UI-Plugin aus, dessen Backend-Teil deaktiviert ist (gelöst im Sample-Code, nicht in Plugwright; siehe R-17).

---

## M3 Start und Test

### R-31 Asynchroner Start-Hook

**Beschreibung:** Plugins können asynchrone Startaufgaben registrieren, die nach `Build()`, in Abhängigkeitsreihenfolge und **vor** allen anderen Hosted Services (auch vor dem Webserver) laufen.

**Vertrag (Vorschlag):**

```csharp
public interface IPluginStartup
{
    Task StartAsync(IServiceProvider services, CancellationToken cancellationToken);
}

// in Configure(PluginContext context)
context.AddStartup<MigrateDataStartup>();   // Typ wird aus DI aufgelöst (Scope je Aufgabe)
```

**Regeln:**

- Umsetzung über **einen** internen Dienst mit `IHostedLifecycleService.StartingAsync`, damit alle Startaufgaben vor jedem `StartAsync` anderer Hosted Services laufen. Prüfe per Test, dass der Webserver vorher keine Anfragen annimmt.
- Reihenfolge: Abhängigkeitsreihenfolge der Plugins, innerhalb eines Plugins Registrierungsreihenfolge. Streng nacheinander, nicht parallel.
- Jede Aufgabe läuft in einem eigenen DI-Scope.
- Fehler: Pflicht-Plugin → Exception, Host startet nicht. Optionales Plugin → Katalog `Failed`, Startaufgaben abhängiger Plugins werden übersprungen (deren Zustand ebenfalls `DependencyUnavailable`). Ihre Dienste bleiben registriert, weil der Container unveränderlich ist. Das wird dokumentiert; Plugins können ihren Zustand über `IPluginCatalog` prüfen.
- Abbruch über das `CancellationToken` des Hosts. Dauer jeder Aufgabe wird geloggt.
- Ausdrücklich **kein** Stopp-Hook. Aufräumen läuft über normale Hosted Services oder `IAsyncDisposable`.

**Akzeptanzkriterien:**

- [ ] Test: Startaufgaben von A, B (abhängig von A) laufen in der Reihenfolge A, B.
- [ ] Test: Webserver beantwortet keine Anfrage, bevor alle Startaufgaben fertig sind.
- [ ] Test: Fehler in optionalem Plugin → App läuft, Katalog `Failed`, abhängige Aufgabe übersprungen.
- [ ] Test: Fehler in Pflicht-Plugin → Host-Start schlägt fehl.
- [ ] Doku-Seite „Startup tasks“, mit Abgrenzung zu `IHostedService`.

### R-40 Testunterstützung für Web-Hosts

**Beschreibung:** Hosts mit Plugwright lassen sich mit `WebApplicationFactory` testen, inklusive gezielt deaktivierter oder fehlerhafter Plugins.

**Regeln:**

- Deaktivieren im Test über die normale Konfiguration (`ConfigureAppConfiguration` bzw. `UseSetting("Plugwright:Plugins:{Id}:Enabled", "false")`). Dafür ist keine eigene Test-API nötig, aber es muss funktionieren und dokumentiert sein.
- Für Fehlerszenarien liefert `tests/TestPlugins` ein Plugin, das per Einstellung in `Configure` bzw. im Start-Hook wirft. Es wird als Muster in der Doku gezeigt.
- Prüfe, ob ein kleines Paket `Plugwright.Testing` nötig ist. Nur anlegen, wenn sich ein echter Mehrwert ergibt, und die Entscheidung ins Entscheidungslog schreiben.

**Akzeptanzkriterien:**

- [ ] Integrationstests in `Plugwright.IntegrationTests` nutzen `WebApplicationFactory` für die Szenarien aus R-10 bis R-13 und R-31.
- [ ] Doku-Seite „Testing apps that use Plugwright“.

### R-41 Doku für den Entwicklungsalltag

**Beschreibung:** Anleitung für Plugins per Projektreferenz in einer Solution.

**Inhalt:**

- Solution-Aufbau mit getrenntem API- und Web-Host: Backend-Teile als Klassenbibliotheken, UI-Teile als Razor Class Libraries, gemeinsame Abstractions-Projekte (DTOs) dazwischen.
- Debuggen mit Breakpoints in Plugins, `dotnet watch` und Hot Reload für Razor-Komponenten in Plugins.
- Wie ein Plugin app-eigene Erweiterungspunkte bereitstellt (Beispiel: Menüeinträge über ein app-eigenes `INavigationItem`).
- Plugwright als Git-Submodul mit Projektreferenz statt NuGet-Paket während gemeinsamer Entwicklung.

**Akzeptanzkriterien:**

- [ ] How-to-Seiten vorhanden, Codebeispiele aus `samples/` eingebunden und im CI gebaut.

---

## M4 Später (erst nach Rücksprache)

Entschieden am 2026-10-11, Details im Entscheidungslog der Spezifikation: R-52, R-53, R-50, R-15 und R-32 werden gebaut, R-51 nach einem Spike, R-14 und R-17 werden nur als Anleitung dokumentiert. Die Tabelle zeigt den ursprünglichen Wunsch.

| Id | Anforderung | Kurzbeschreibung |
|---|---|---|
| R-14 | Austauschbare Quelle für den Aktivierungszustand | `IPluginActivationSource`, ausgewertet in `ApplyPlugins()`, Standard ist die Konfiguration aus R-10. Damit kann der Host den Zustand z. B. aus einer eigenen Verwaltung lesen. Pflicht-Plugins bleiben unberührt. |
| R-17 | Gegenstück in einem anderen Host | Ein Plugin deklariert, dass es ein Gegenstück in einem anderen Host braucht (z. B. UI-Teil → Backend-Id). Der Host prüft das beim Start über R-16 und behandelt ein fehlendes Gegenstück wie eine nicht verfügbare Abhängigkeit (R-11). Offene Frage: Verhalten, wenn der andere Host beim Start nicht erreichbar ist. |
| R-15 | Optionale Abhängigkeiten | bereits als v2 geplant; Plugin lädt auch ohne die Abhängigkeit, erfährt über den Katalog, ob sie da ist |
| R-32 | Neustart anstoßen, auch unter IIS | bestehender v2-Plan (Neustart-Dienst) um IIS ergänzen: `StopApplication()` beendet den Prozess, IIS startet beim nächsten Request neu; Verhalten für in-process und out-of-process dokumentieren |
| R-50 | Health Check „Plugins“ | `AddPlugwrightHealthCheck()`: `Healthy`, wenn alle Plugins geladen sind; `Degraded`, wenn optionale fehlen oder fehlgeschlagen sind; Daten je Plugin im Ergebnis |
| R-51 | Statische Web-Assets dynamisch geladener Plugins | `_content/…` aus Plugin-Verzeichnissen bzw. Paketen ausliefern, inkl. Integritätsprüfung der Dateien |
| R-52 | Vom Host festgelegte geteilte Assemblies | `builder.Plugins.ShareAssembly(typeof(X).Assembly)` bzw. Präfixe; diese werden beim dynamischen Laden aus dem Standard-Kontext genommen (z. B. App-Abstractions, UI-Bibliotheken, EF Core) |
| R-53 | Assembly-Scan | bereits in der Roadmap; alle Plugins aus referenzierten Assemblies finden |

## 6. Nicht im Umfang

- Hot Reload oder Entladen von Plugins zur Laufzeit (bleibt ausgeschlossen).
- App-spezifische Erweiterungspunkte wie Menüs, Datentypen oder Rechte. Die definiert die Host-Anwendung selbst, Plugwright transportiert sie nur über DI.
- Datenhaltung von Plugins (Datenbankschemata, Migrationen). Plugwright stellt nur den Start-Hook (R-31) bereit.
- Blazor WebAssembly. Nur Blazor Server bzw. Interactive Server ist Ziel.

## 7. Offene Punkte zur Entscheidung während der Umsetzung

1. Liegen `IWebPlugin` und `IBlazorPlugin` in `Plugwright.Abstractions` oder in eigenen Abstractions-Paketen?
2. Werden Plugin-Instanzen zwischen Phase 1 und Phase 2 geteilt (R-03)? Bisher instanziiert der Standard-Handler in Phase 1.
3. Lassen sich Dienste eines in `Configure` fehlgeschlagenen optionalen Plugins zuverlässig zurücknehmen (R-13), z. B. über eine Zwischen-`ServiceCollection`?
4. Event-IDs und Kategorienamen für die neuen Log-Events.

## 8. Startprompt für Claude Code

```text
Lies CLAUDE.md, docs/SPEC.md und docs/requirements/REQUIREMENTS-web-host.md vollständig.
Erstelle einen Umsetzungsplan für Meilenstein M1 (R-01 bis R-05): betroffene Projekte,
neue Pakete, öffentliche API mit Signaturen, Tests, Samples und Doku-Seiten.
Beantworte dabei die offenen Punkte 1 und 2 aus Abschnitt 7 mit einer Empfehlung.
Warte auf meine Freigabe, bevor du Code schreibst.
```
