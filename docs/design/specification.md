# Plugwright – Spezifikation des Plugin-Systems für den .NET Generic Host

Oct 6, 2026 · @Yannick

## Zweck und Vision

Ziel ist eine öffentliche C#-Bibliothek, die ein generisches Plugin-System auf Basis von `IHost` und `IHostApplicationBuilder` bereitstellt. Dieses Dokument ist die Spezifikation dafür und dient später als Ausgangspunkt für die Umsetzung mit Claude Code.

- Plugins erweitern eine Host-Anwendung über Dependency Injection, z. B. durch zusätzliche Services oder Hosted Services.
- Der Einstieg ist bewusst klein: eine Konsolenanwendung mit dem .NET Generic Host.
- Die Bibliothek baut auf den Standard-Abstraktionen von Microsoft auf, statt eigene Host-Konzepte zu erfinden.
- Eine sehr gute, ausführliche Anwenderdokumentation ist Kernanforderung, nicht Beiwerk.

Das Dokument ist ein Arbeitsstand und wird über mehrere Sessions ergänzt. Einträge mit **Vorschlag** sind noch nicht entschieden.

## Rahmenbedingungen

Fest steht: C#-Klassenbibliothek, öffentlich auf GitHub, aufgebaut auf `Microsoft.Extensions.Hosting`. Alles andere ist noch Vorschlag.

| Thema | Stand | Inhalt |
| --- | --- | --- |
| Sprache | Festgelegt | C# |
| Projektart | Festgelegt | Klassenbibliothek, verteilt als NuGet-Paket(e) |
| Veröffentlichung | Festgelegt | Öffentliches GitHub-Repository |
| Basis | Festgelegt | `IHost`, `IHostApplicationBuilder` aus `Microsoft.Extensions.Hosting` |
| Ziel-Framework | Festgelegt | .NET 10 (LTS); ermöglicht C# 14 Extension Members |
| Lizenz | Festgelegt | MIT |
| CI/CD | Festgelegt | GitHub Actions ab dem ersten Tag: Build, Tests und DocFX-Build (Codebeispiele werden mitkompiliert) bei jedem Push und PR; NuGet-Publish folgt später |
| Paketschnitt | Festgelegt | `*.Abstractions` nur mit Plugin-Vertrag (`IPlugin`, `PluginContext`, Metadaten, Attribute); Hauptpaket mit Extensions, `IPluginHandler`, `PluginDescriptor`, `IPluginVerifier` und Laden; NuGet-Quelle als eigenes Paket \*.NuGet |
| Projektname | Festgelegt | Plugwright; Pakete z. B. Plugwright, Plugwright.Abstractions, Plugwright.NuGet, Plugwright.Cli |

## Begriffe

Diese Begriffe gelten im gesamten Projekt, auch in Code, Doku und Issues.

| Begriff | Bedeutung |
| --- | --- |
| Host-Anwendung | Die Anwendung, die das Plugin-System nutzt und Plugins lädt |
| Plugin | Eine Klasse, die ein Handler verarbeitet; im Standardfall implementiert sie `IPlugin` |
| Plugin-Assembly | Eine DLL, die ein oder mehrere Plugins enthält; trägt deren gemeinsame Identität, Version und Abhängigkeiten |
| Plugin-Quelle | Woher Plugins kommen: direkt in Program.cs, DLL-Verzeichnis oder NuGet-Ordner |
| Handler | Komponente, die weiß, was mit einem Plugin-Typ in Phase 1 und Phase 2 passiert |
| `builder.Plugins` | Extension Property; legt fest, welche Plugins geladen werden |
| Phase 1 (`ApplyPlugins`) | Vor `Build()`: Handler registrieren Services, der Container existiert noch nicht |
| Phase 2 (`UsePlugins`) | Nach `Build()`: Container steht, Handler arbeiten mit der fertigen App |

## Architektur: Extension Members und zwei Phasen

**Entschieden:** Das Plugin-System besteht nur aus C# 14 Extension Members auf `IHostApplicationBuilder` und `IHost`, ohne eigene Builder- oder App-Klasse. Jede Anwendung, ob Konsole, Worker oder Web, nutzt ihren normalen Builder und ruft das Plugin-System an zwei festen Stellen explizit auf.

### Betrachtete Varianten

| Variante | Idee | Vorteile | Nachteile |
| --- | --- | --- | --- |
| A: Wrapper-Klassen | `PluginAppBuilder : IHostApplicationBuilder` kapselt `HostApplicationBuilder`; `PluginApp : IHost` kapselt den gebauten Host | Klarer Einstieg, eigene Properties wie `Plugins` möglich, bekanntes Muster aus ASP.NET Core | Delegationscode für alle Interface-Member; Nutzer mit eigenem Builder müssen umsteigen |
| B: Extension Methods | `builder.AddPlugins(...)` auf jedem `IHostApplicationBuilder` | Funktioniert mit jedem Builder (auch Web, Worker) | Kein Ort für Zustand; Plugin-Liste muss über `Properties` oder DI transportiert werden |
| C: C# 14 Extension Members | Extension-Property `builder.Plugins` auf `IHostApplicationBuilder` | Sieht aus wie ein echtes Property, funktioniert mit jedem Builder | Braucht C# 14 / .NET 10; Zustand liegt trotzdem in `Properties` |
| A + C kombiniert | Wrapper als Standardweg, Extension Members für fremde Builder | Beide Zielgruppen bedient | Zwei Wege, die gleich dokumentiert und getestet werden müssen |

Gewählt wurde C. Wrapper-Klassen (A) hätten nur den Vorteil gehabt, `Build()` selbst zu kontrollieren; der entfällt durch die expliziten Aufrufe.

### Nutzung, gleich für alle Host-Typen

```csharp
var builder = Host.CreateApplicationBuilder(args); // oder WebApplication.CreateBuilder(args)

builder.Plugins.Add<MyPlugin>();
builder.Plugins.AddFromDirectory("plugins");

builder.ApplyPlugins();   // Phase 1: vor Build()

var app = builder.Build();
app.UsePlugins();         // Phase 2: nach Build()

await app.RunAsync();
```

| Aufruf | Erweitert | Aufgabe |
| --- | --- | --- |
| `builder.Plugins` | `IHostApplicationBuilder` (Extension Property) | Legt fest, welche Plugins es gibt; speichert nur Typen und Quellen |
| `builder.ApplyPlugins()` | `IHostApplicationBuilder` | Phase 1: Handler registrieren Services; Plugin-Liste wird als Singleton im DI-Container abgelegt |
| `app.UsePlugins()` | `IHost` | Phase 2: Handler arbeiten mit der gebauten App, z. B. Middleware oder Endpoints |

### Warum explizit, auch bei der Konsole

- `Build()` gehört dem jeweiligen Builder; Extension Members können den Aufruf nicht abfangen.
- Bei Web bestimmt die Position von `UsePlugins()` die Reihenfolge in der Middleware-Pipeline.
- Ein Muster für alle Host-Typen, passend zur .NET-Konvention `AddX` / `UseX` / `MapX`.
- Verworfen: automatische Auslösung über eine eigene `IServiceProviderFactory`. Sie kollidiert mit Containern wie Autofac und ist für Nutzer schwer zu durchschauen.

### Schutz vor Fehlbedienung

- `UsePlugins()` ohne vorheriges `ApplyPlugins()` wirft eine verständliche Exception.
- `Plugins.Add…()` nach `ApplyPlugins()` wirft eine Exception.
- Doppelter Aufruf von `ApplyPlugins()` oder `UsePlugins()` wirft ebenfalls.

### Später für Web

Möglich ist eine Aufteilung in `UsePlugins()` für Middleware und `MapPlugins()` für Endpoints, analog zu ASP.NET Core. Für v1 reicht ein Aufruf.

### Was `IHostApplicationBuilder` bereits bietet

`Services`, `Configuration`, `Environment`, `Logging`, `Metrics`, `Properties` und `ConfigureContainer`. Handler und Plugins nutzen genau diese Member, statt eigene Parallelwege zu bekommen.

## Handler-Pipeline

`Plugins.Add…()` speichert nur Plugin-Typen. **Entschieden:** Austauschbare Handler entscheiden, was mit einem Typ in Phase 1 und Phase 2 passiert. Neue Plugin-Arten wie Web, Blazor oder eigene kommen als neue Handler dazu, ohne den Kern zu ändern.

```csharp
public interface IPluginHandler
{
    bool CanHandle(Type pluginType);

    // Phase 1, aufgerufen von builder.ApplyPlugins()
    void Apply(PluginDescriptor plugin, IHostApplicationBuilder builder);

    // Phase 2, aufgerufen von app.UsePlugins()
    void Use(PluginDescriptor plugin, IHost app);
}
```

| Handler | Paket | Phase 1 (`ApplyPlugins`) | Phase 2 (`UsePlugins`) |
| --- | --- | --- | --- |
| Standard | Kern | Instanziiert `IPlugin` und ruft `Configure(context)` auf | – |
| Web | `*.AspNetCore`, später | Services registrieren | Middleware und Endpoints an `WebApplication` |
| Blazor | später | Nav-Items als DI-Services registrieren | Komponenten-Assemblies für das Routing bekannt machen |
| Eigene | Nutzer | Frei | Frei |

### Regeln

- Handler werden getrennt von Plugins direkt am Builder registriert: `builder.PluginHandlers.Add<T>()`. Der Standard-Handler für `IPlugin` ist immer registriert.
- Handler laufen in Phase 1 vor dem Container und werden daher direkt instanziiert, nicht aus DI aufgelöst.
- Alle passenden Handler verarbeiten einen Plugin-Typ, in Registrierungsreihenfolge, der Standard-Handler zuerst.
- Beim Laden aus einem Verzeichnis gelten nur Typen als Plugin, die mindestens ein Handler verarbeiten kann.
- Ein explizit hinzugefügter Typ ohne zuständigen Handler führt zu einer Exception mit Hinweis auf die fehlende Handler-Registrierung. Die Option „überspringen und loggen“ gilt auch hier.
- In v1 kommen Handler nur aus der Host-Anwendung, nicht aus Plugin-Assemblies. Sonst würde ein geladenes Plugin bestimmen, wie andere Plugins behandelt werden.
- Die Pipeline erzeugt genau eine Instanz je Plugin-Typ (parameterloser Konstruktor) und hält sie in `PluginDescriptor.Instance`. Alle Handler nutzen diese Instanz in beiden Phasen.
- `IPluginHandler` und `PluginDescriptor` liegen im Hauptpaket, nicht in Abstractions, da Handler nur aus der Host-Anwendung kommen.
- `PluginDescriptor.Instance` ist vom Typ `object`, da Plugins nicht `IPlugin` implementieren müssen. Für Handler, die ein bestimmtes Interface verarbeiten, gibt es die Basisklasse `PluginHandler<TPlugin>`: Sie implementiert `CanHandle` und übergibt die Instanz typisiert.

### Logging in Phase 1

In Phase 1 existiert noch kein `ILoggerFactory`. **Entschieden:** Log-Einträge aus Phase 1 (Pipeline, Prüfer, `PluginContext.Logger`) werden gepuffert und ausgegeben, sobald der Container steht, in `UsePlugins()`. Bricht Phase 1 per Fail-fast ab, trägt die Exception alle nötigen Informationen selbst.

## Plugin-Vertrag und Lebenszyklus

**Entschieden:** Ein schlankes Interface mit genau einer Pflichtmethode, die in Phase 1 ein Kontextobjekt erhält. Laufzeitverhalten kommt über normale DI-Mittel wie `IHostedService`, nicht über eigene Plugin-Hooks.

```csharp
public interface IPlugin
{
    void Configure(PluginContext context);
}

public sealed class HeartbeatPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services
            .AddOptions<HeartbeatOptions>()
            .Bind(context.Configuration)   // eigener Settings-Abschnitt
            .ValidateOnStart();

        context.Builder.Services.AddHostedService<HeartbeatService>();
    }
}
```

`PluginContext` enthält den Builder, den eigenen Konfigurationsabschnitt, die Metadaten des Plugins und einen `ILogger` (gepuffert, siehe Logging in Phase 1). Neue Eigenschaften lassen sich später ergänzen, ohne `IPlugin` zu brechen. Deshalb steht diese Signatur vor 1.0 fest.

### Ablauf

1. `builder.Plugins.Add…()` speichert Plugin-Typen und Quellen.
2. `builder.ApplyPlugins()` findet die Typen und lädt bei Bedarf ihre Assemblies.
3. Die Pipeline instanziiert jedes Plugin genau einmal (parameterloser Konstruktor, da der Container noch nicht existiert). Die zuständigen Handler führen Phase 1 aus; der Standard-Handler ruft `Configure(context)` auf.
4. `builder.Build()` erzeugt Host und Container.
5. `app.UsePlugins()` lässt Handler Phase 2 ausführen, sofern sie eine haben.
6. Start und Stopp laufen über den normalen Host-Lebenszyklus der registrierten Hosted Services.

### Offene Punkte

- Metadaten und Abhängigkeiten: siehe Abschnitt Plugin-Metadaten und Abhängigkeiten.
- Verhalten bei Fehlern in `Configure`: entschieden – Fail-fast als Standard, per Option überspringen und loggen.
- Die Option ist ein globaler Schalter, z. B. `builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog`. Er gilt für Fehler in `Configure`, fehlende Handler und fehlgeschlagene Prüfung. Der genaue Name ist vorläufig.

## Konfiguration je Plugin

**Entschieden:** Jedes Plugin hat einen eigenen Konfigurationsabschnitt. Plugin-Einstellungen liegen in einer eigenen Datei `pluginsettings.json`, die die App selbst verwaltet. `appsettings.json` bleibt Sache von Entwicklung und Betrieb.

```json
{
  "Plugwright": {
    "Plugins": {
      "Acme.Heartbeat": {
        "Settings": { "Interval": "00:00:10" }
      }
    }
  }
}
```

### Regeln

- Struktur `Plugwright:Plugins:{Plugin-ID}`. Plugin-eigene Werte liegen unter `Settings`; daneben ist Platz für Werte des Plugin-Systems wie `Enabled` in v2.
- `ApplyPlugins()` bindet `pluginsettings.json` als JSON-Konfigurationsanbieter ein. Derselbe Abschnitt darf auch in `appsettings.json` stehen; beides wird zusammengeführt.
- `pluginsettings.json` wird zuletzt eingebunden und gewinnt bei Konflikten, auch gegenüber Umgebungsvariablen. Die Doku nennt diese Abweichung von der üblichen Reihenfolge deutlich.
- Der Anbieter für `pluginsettings.json` übernimmt nur Schlüssel unter `Plugwright:Plugins`. So kann die von der App beschreibbare Datei weder andere Einstellungen noch Prüfer-Einstellungen überschreiben.
- Der Pfad ist konfigurierbar, Standard ist neben der App. Die Doku zeigt beschreibbare Orte, z. B. `ProgramData` unter Windows oder `/var/lib` unter Linux.
- Gespeichert wird über `IPluginSettingsStore`. Die Datei gilt als maschinenverwaltet; Kommentare gehen beim Speichern verloren.
- Änderungen werden live erkannt (`reloadOnChange`). Plugins mit `IOptionsMonitor` sehen neue Werte ohne Neustart. Neu geladen werden nur Einstellungen, keine Plugins.
- Plugins lesen ihren Abschnitt über `PluginContext.Configuration` und binden ihn per Options-Pattern.
- Mehrere Plugin-Klassen einer Assembly teilen sich ihren Abschnitt, da die ID auf Ebene der Assembly liegt. Bei Bedarf nutzen sie Unterschlüssel.
- Plugin-IDs mit Punkten sind in Linux-Umgebungsvariablen umständlich; die Doku zeigt Alternativen.

## Plugin-Discovery und Laden

**Entschieden:** v1 unterstützt drei Plugin-Quellen: direkt in `Program.cs`, DLLs aus einem Verzeichnis und NuGet-Pakete aus einem Ordner. DLL- und NuGet-Quellen bekommen eine Integritätsprüfung beim Laden (Hash, optional Zertifikat), siehe Abschnitt Integritätsprüfung. Die API-Namen sind vorläufig.

| Quelle | Beispiel-API | Laden | Einsatz |
| --- | --- | --- | --- |
| Direkt | `Plugins.Add<MyPlugin>()` | Kein dynamisches Laden | Plugins im selben Projekt oder als Paket referenziert |
| DLL-Verzeichnis | `Plugins.AddFromDirectory("plugins")` | Eigener `AssemblyLoadContext` je Plugin-Ordner | Erweiterung ohne Neukompilieren |
| NuGet-Ordner | `Plugins.AddFromPackages("packages")` | `.nupkg` wird entpackt, dann wie DLL geladen | Plugins mit Version und Paketmetadaten ausliefern |

### Offen zum NuGet-Ordner

- Paketabhängigkeiten auf Drittpakete: müssen sie im Ordner liegen, oder darf aus einem Feed nachgeladen werden?
- Wohin wird entpackt, und wann wird der Cache aufgeräumt?

### Aufbau des DLL-Verzeichnisses

**Entschieden:** Jedes Plugin liegt in einem eigenen Unterordner, die Haupt-Assembly trägt den Namen des Ordners: `plugins/<Name>/<Name>.dll`. Der Ordner ist die Einheit für Hashes, Manifest und Ladekontext. DLLs direkt in `plugins/` werden ignoriert, mit Hinweis im Log.

- Ein relativer Pfad bezieht sich auf `builder.Environment.ContentRootPath`, wie `appsettings.json`.
- Fehlt das Verzeichnis, gibt es eine Warnung im Log und es werden keine Plugins daraus geladen.
- Ein Unterordner ohne `<Name>.dll`, mit nicht ladbarer Assembly oder ohne Plugin-Typ ist ein Plugin-Fehler (Exception bzw. überspringen und loggen).
- Plugin-Typen sind die öffentlichen, nicht abstrakten Klassen der Haupt-Assembly, die ein registrierter Handler verarbeiten kann.
- Gemeinsame Assemblies (`Plugwright*`, `Microsoft.Extensions.*`) kommen aus dem Standard-Kontext in der Version des Hosts, auch wenn das Plugin eigene Kopien mitbringt. Hat der Host eine solche Assembly nicht, wird die Kopie des Plugins geladen.
- Plugins werden in der Reihenfolge der Quellen verarbeitet, innerhalb eines Verzeichnisses alphabetisch nach Ordnername.

### Ladeweg

**Entschieden:**

- Ohne registrierte Prüfer wird per `LoadFromAssemblyPath` geladen; `Assembly.Location` funktioniert.
- Mit Prüfern wird per `LoadFromStream` aus den geprüften Bytes geladen; `Assembly.Location` ist dann leer. Die Doku nennt das.
- Native Bibliotheken werden per Hash geprüft und direkt danach vom Pfad geladen. Das verbleibende kleine Zeitfenster nennt die Doku.

### Wichtige Punkte beim dynamischen Laden

- Geteilte Typen wie der Plugin-Vertrag und `Microsoft.Extensions.*` müssen aus dem Standard-Kontext kommen. Sonst sind Typen zwar gleich benannt, aber nicht kompatibel.
- Abhängigkeiten eines Plugins werden über `AssemblyDependencyResolver` und dessen `.deps.json` aufgelöst.
- Entladen von Plugins zur Laufzeit ist nicht geplant; Aktivieren und Deaktivieren wirkt nach Neustart (siehe Roadmap v2).
- Plugins laufen mit voller Vertrauensstellung im Prozess. Das muss die Doku klar sagen.

## Plugin-Metadaten und Abhängigkeiten

**Entschieden:** Identität, Version und Abhängigkeiten liegen auf Ebene von Assembly bzw. NuGet-Paket und kommen primär aus deren eigenen Metadaten. Code-Attribute sind nur der Fallback für das, was Paket und Assembly nicht ausdrücken.

### Herkunft der Metadaten

| Quelle | Identität und Version | Beschreibung, Autor | Abhängigkeiten |
| --- | --- | --- | --- |
| NuGet-Ordner | `.nuspec`: ID, Version | `.nuspec` | `.nuspec` inkl. Versionsbereichen, plus Assembly-Referenzen |
| DLL-Verzeichnis | Assembly-Name, Assembly-Version | Assembly-Attribute aus der `.csproj` | Assembly-Referenzen |
| Direkt in `Program.cs` | Assembly-Name, Assembly-Version | Assembly-Attribute aus der `.csproj` | Assembly-Referenzen |
| Fallback, alle Quellen | – | `[Plugin(...)]` | `[PluginDependency(...)]` für Abhängigkeiten ohne Assembly-Referenz |

Metadaten und Referenzen werden gelesen, ohne Plugin-Code auszuführen, über `MetadataLoadContext` (Paket `System.Reflection.MetadataLoadContext`, als Abhängigkeit erlaubt). Abhängigkeiten und Integrität sind damit geprüft, bevor Code läuft.

```csharp
// Nur nötig, wenn Acme.Logging nicht direkt referenziert wird oder eine Mindestversion gelten soll
[assembly: PluginDependency("Acme.Logging", MinVersion = "1.2.0")]
```

- `[Plugin]` und `[PluginDependency]` gelten nur auf Assembly-Ebene.
- Die Version ist eine semantische Version aus `<Version>` (`AssemblyInformationalVersion` ohne `+…`), verglichen nach SemVer 2.0. `[PluginDependency]` kennt nur `MinVersion`.
- Eine Referenz auf die Assembly eines anderen Plugins ist eine Abhängigkeit; geprüft wird zusätzlich, dass dessen `AssemblyVersion` mindestens der referenzierten entspricht.
- Setzt das Projekt weder `<Authors>` noch `<Company>`, trägt das SDK den Assembly-Namen ein; das gilt als nicht gesetzt, `[Plugin(Authors = …)]` füllt dann die Lücke.
- Plugins aus dem Code können nur von Plugins aus dem Code abhängen.

### Regeln für Abhängigkeiten

- Eine Abhängigkeit bedeutet Vorhandensein, Reihenfolge und Typen teilen.
- **Vorhandensein:** Fehlt eine Abhängigkeit oder passt ihre Version nicht, gibt es eine Exception.
- **Reihenfolge:** Abhängigkeiten werden in Phase 1 und 2 vor dem abhängigen Plugin verarbeitet. Zyklen führen zu einer Exception, die den Zyklus nennt.
- **Typen teilen:** Der Ladekontext eines Plugins reicht Assemblies seiner Abhängigkeiten an deren Ladekontext weiter, damit Typen kompatibel sind.
- NuGet-Abhängigkeiten müssen im Ordner gefunden werden, entweder als Plugin-Paket oder als Bibliothekspaket.
- Mehrere Plugin-Klassen in einer Assembly teilen Identität und Abhängigkeiten und werden gemeinsam geladen.
- Doppelte IDs, etwa dasselbe Plugin in zwei Verzeichnissen oder im Code und im Verzeichnis, führen immer zu einer Exception, die beide Quellen nennt; „überspringen und loggen“ gilt hier nicht. IDs werden ohne Beachtung der Groß-/Kleinschreibung verglichen, wie Konfigurationsschlüssel.
- Bei „überspringen und loggen“ wird ein Plugin samt aller (transitiv) davon abhängigen Plugins übersprungen, jedes mit eigenem Log-Eintrag.
- Optionale Abhängigkeiten kommen erst in v2.

### Weitere Festlegungen

- Attribute ergänzen nur, sie überschreiben nie. Nennen Paket und Attribut dieselbe Abhängigkeit mit unterschiedlichen Versionen, müssen beide Bedingungen erfüllt sein. Bei beschreibenden Feldern gewinnt Paket bzw. Assembly.
- NuGet-Pakete werden mit `NuGet.Packaging` gelesen (Versionsbereiche, Auswahl des `lib/`-Ordners, Paketsignaturen). Die NuGet-Quelle liegt in einem eigenen Paket `*.NuGet`, damit der Kern ohne NuGet-Abhängigkeiten bleibt.

## Integritätsprüfung

**Entschieden:** Integritätsprüfung ist optional. Der Anwender registriert Prüfer explizit, auch die mitgelieferten Hash- und Zertifikatsprüfer. Ist kein Prüfer registriert, findet keine Prüfung statt; beim Start erscheint ein Hinweis im Log.

```csharp
builder.PluginVerifiers.AddHashVerifier(options => options.LockFile = "/etc/myapp/plugins.lock.json");
builder.PluginVerifiers.AddCertificateVerifier(options => ...);
builder.PluginVerifiers.Add(new MyCompanyVerifier());   // eigener Prüfer
```

| Modus | NuGet-Ordner | DLL-Verzeichnis | Pflege bei Updates |
| --- | --- | --- | --- |
| Hash | Paket-Hash in einer Lock-Datei auf Host-Seite | SHA-256 aller Dateien des Plugin-Ordners in der Lock-Datei | Lock-Datei bei jedem Update neu erzeugen |
| Zertifikat | NuGet-Paketsignatur, geprüft über `NuGet.Packaging` | Signiertes Manifest je Plugin-Ordner: SHA-256 aller Dateien, CMS-signiert über `System.Security.Cryptography.Pkcs` | Keine, solange derselbe Herausgeber signiert |

### Regeln

- Vertraut wird über Herausgeber-Zertifikate bzw. über die Lock-Datei. Beides liegt auf Host-Seite, nicht im Plugin-Ordner.
- Zertifikate werden per Fingerabdruck oder über einen vertrauenswürdigen Aussteller (z. B. Firmen-CA) plus Einschränkung auf den Antragsteller (Subject) akzeptiert. So bricht eine Zertifikatserneuerung nichts.
- Das signierte Manifest enthält Plugin-ID, Version und alle Dateien. Dateien im Plugin-Ordner, die nicht im Manifest stehen, führen zum Fehler.
- Geprüft werden alle Dateien eines Plugins, nicht nur die Haupt-DLL.
- Geladen wird aus den geprüften Bytes, z. B. per `LoadFromStream`, damit zwischen Prüfung und Laden nichts getauscht werden kann.
- Der entpackte NuGet-Cache wird bei jedem Start gegen die Hashes aus dem geprüften Paket geprüft oder neu entpackt.
- Hashes tragen eine Algorithmus-Kennung, z. B. `sha256-…`, damit der Algorithmus später wechseln kann.
- Die Prüfung läuft vor dem Lesen der Metadaten per `MetadataLoadContext`.
- Fehlgeschlagene Prüfung führt zu einer Exception; „überspringen und loggen“ gilt wie bei anderen Ladefehlern.
- Alle Prüfer implementieren `IPluginVerifier`, mitgelieferte und eigene gleichermaßen. Jeder Prüfer gibt über `CanVerify` an, welche Quellen er prüfen kann. Alle zuständigen Prüfer müssen bestehen. Sind Prüfer registriert, aber keiner ist für ein Plugin zuständig, gibt es eine Exception. Prüfer-Einstellungen wie Lock-Datei-Pfad und vertrauenswürdige Zertifikate kommen standardmäßig aus dem Code. Optional bindet der Entwickler sie ausdrücklich an einen Konfigurationsabschnitt, z. B. `builder.PluginVerifiers.Add<CertificateVerifier>(builder.Configuration.GetSection("Plugwright:Verification:Certificate"))`. Welche Prüfer es gibt, legt immer der Code fest.
- Jede Prüfung erzeugt ein strukturiertes Log-Event mit fester Event-ID (Plugin, Version, Modus, Zertifikat, Ergebnis). Die Ergebnisse sind zusätzlich per API abrufbar.
- Strong-Name-Signaturen werden nicht genutzt; laut Microsoft sind sie kein Sicherheitsmerkmal.

### Hinweis für die Doku

Die Prüfung schützt nur, wenn Lock-Datei und Vertrauenskonfiguration nicht von denselben Personen geändert werden können wie der Plugin-Ordner. Liegen Prüfer-Einstellungen in `appsettings.json`, gilt das auch für diese Datei.

### CLI-Tool

v1 enthält ein CLI-Tool als `dotnet tool` mit drei Aufgaben:

1. Lock-Datei für den Hash-Modus erzeugen.
2. Manifest eines Plugin-Ordners signieren.
3. `verify`: Plugin-Ordner gegen die Prüfkonfiguration prüfen, ohne die App zu starten, z. B. in CI oder beim Deployment.

### Weitere Festlegungen

- Mitgelieferte Prüfer haben eigene Registrierungsmethoden (`AddHashVerifier`, `AddCertificateVerifier`) mit Einstellungen per Delegate oder per ausdrücklich übergebenem Konfigurationsabschnitt. Eigene Prüfer über `Add(instance)` oder `Add<T>()`. Je Typ ein Prüfer.
- Plugins aus dem Code werden nie geprüft; die Regel „kein zuständiger Prüfer → Fehler“ gilt nur für Plugin-Ordner (und später NuGet).
- Prüffehler, fehlender zuständiger Prüfer, Ausnahmen eines Prüfers und eine fehlende oder ungültige Lock-Datei sind Plugin-Fehler (Exception bzw. überspringen und loggen).
- Lock-Datei: JSON mit `version` und je Plugin-Ordner (Name ohne Beachtung der Groß-/Kleinschreibung) allen Dateien mit `sha256-{base64}`. Standardpfad `plugins.lock.json` im Content Root.
- Ergebnisse per API: `PluginReport` im DI-Container mit aktiven, übersprungenen Plugins und allen Prüfergebnissen.
- Zertifikate: in v1 keine Zeitstempel; das Zertifikat muss zum Prüfzeitpunkt gültig sein. Sperrlistenprüfung einstellbar (`NoCheck`, `Online`, `Offline`), Standard `NoCheck`.
- CLI: Paket `Plugwright.Cli`, Befehl `plugwright`. `lock` wurde vorgezogen, damit Doku und Beispiele zur Prüfung ein echtes Werkzeug nutzen. Argumente werden ohne zusätzliche Bibliothek ausgewertet.

## Scope v1 und Roadmap

**Vorschlag:** v1 liefert eine Konsolen-App mit Plugins aus Program.cs, DLL-Verzeichnis und NuGet-Ordner, vollständig dokumentiert.

### In v1

- [ ] `builder.Plugins`, `ApplyPlugins()` und `UsePlugins()` lauffähig, inklusive Handler-Pipeline und Standard-Handler
- [ ] `IPlugin`-Vertrag mit PluginContext im Abstractions-Paket; Konfiguration je Plugin über pluginsettings.json
- [ ] Quellen: direkt in Program.cs, DLL-Verzeichnis, NuGet-Ordner, jeweils mit Integritätsprüfung und CLI-Tool
- [ ] Beispiel-Plugin, das einen Hosted Service hinzufügt
- [ ] Plugin-Metadaten, Abhängigkeiten zwischen Plugins und Logging der geladenen Plugins beim Start
- [ ] Unit- und Integrationstests
- [ ] Anwenderdokumentation und Beispielprojekte

### Nicht in v1

- Hot Reload und Entladen von Plugins
- Andere Host-Typen (ASP.NET Core, Desktop)

### Danach, in dieser Reihenfolge

1. Optionale Abhängigkeiten zwischen Plugins
2. Assembly-Scan als weitere Quelle
3. Unterstützung für `WebApplicationBuilder` (ASP.NET Core)

### v2: Plugins aktivieren und deaktivieren

**Vorschlag:** Kein Hot Reload. Plugins werden in der App aktiviert oder deaktiviert, die Änderung wirkt nach einem Neustart des Prozesses.

1. Der Zustand (aktiv/inaktiv) wird persistent in pluginsettings.json gespeichert.
2. `ApplyPlugins()` überspringt deaktivierte Plugins beim nächsten Start.
3. Ein Dienst der Bibliothek stößt den Neustart sauber an, z. B. über `IHostApplicationLifetime.StopApplication()` mit definiertem Exit-Code; Windows-Dienst oder systemd starten neu.

Gründe gegen Hot Reload:

- Die Microsoft-DI ist nach `Build()` unveränderlich.
- Hosted Services, Middleware-Pipeline und Blazor-Komponenten werden einmal beim Start eingerichtet.
- Ein entladbarer `AssemblyLoadContext` wird nur ohne verbleibende Referenzen freigegeben; Lecks bleiben unbemerkt.

Sofortiges Pausieren bleibt Sache des einzelnen Plugins, z. B. per Feature-Flag über `IOptionsMonitor`.

## Dokumentationsstrategie

Dokumentation wird zusammen mit dem Code geschrieben und gilt als Teil jeder fertigen Funktion. **Entschieden:** Aufbau nach dem Diátaxis-Modell, veröffentlicht als statische Seite auf GitHub Pages.

| Bereich | Zweck | Beispiele |
| --- | --- | --- |
| Tutorials | Lernen durch Tun | „Erstes Plugin in 10 Minuten“ |
| How-to-Anleitungen | Konkrete Aufgaben lösen | Hosted Service per Plugin, Plugins aus Verzeichnis laden |
| Referenz | Vollständige API-Beschreibung | Generiert aus XML-Doku-Kommentaren |
| Erklärungen | Hintergründe verstehen | Build- vs. Laufzeit-Phase, Ladekontexte, Designentscheidungen |

### Regeln

- Jeder öffentliche Typ und Member hat XML-Doku-Kommentare, inklusive Beispiel wo sinnvoll.
- Jede How-to-Anleitung verweist auf ein lauffähiges Projekt unter `samples/`.
- Codebeispiele in der Doku werden im CI mitkompiliert, damit sie nicht veralten.
- Im Repo: `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`.

### Werkzeug und Sprache

**Entschieden:** Die Doku ist auf Englisch und wird ausschließlich mit DocFX gebaut, für Anleitungen und API-Referenz gleichermaßen. Alle Inhalte liegen als Markdown im Repo.

- Anleitungen verweisen per `<xref:…>` auf API-Seiten. Kaputte Querverweise lassen den Build scheitern (Warnungen als Fehler).
- Verweise auf .NET-Typen werden über die Microsoft-Learn-Verweisdaten automatisch verlinkt.
- Codebeispiele werden aus Dateien unter `samples/` eingebunden, bei Bedarf als `#region`. Die Doku zeigt damit nur Code, der tatsächlich baut.
- Grundlage ist das moderne DocFX-Template, angepasst über CSS-Variablen. „Zuletzt aktualisiert“ aus Git wird per kleiner Template-Anpassung ergänzt.
- DocFX läuft als `dotnet tool`; das Repo braucht keine Node.js-Toolchain.

### Versionen

**Entschieden:** Zunächst keine Versionsumschaltung.

- Die Doku beschreibt immer die aktuelle Version.
- Neue oder geänderte APIs tragen Hinweise wie „Available since 1.2“ oder „Changed in 2.0“.
- Jede Hauptversion bekommt einen Migrations-Guide; dazu `CHANGELOG.md`.
- Ältere Stände sind über die Git-Tags im Repo erreichbar.
- Eine Versionsumschaltung wird erst erwogen, wenn zwei Hauptversionen parallel gepflegt werden.

## Leitlinien für die Umsetzung mit Claude Code

Aus diesem Abschnitt wird später die `CLAUDE.md` im Repository abgeleitet. **Vorschlag** für den Startstand:

### Repository-Struktur

```
src/        Bibliothek und Abstractions
tests/      Unit- und Integrationstests
samples/    Lauffähige Beispiel-Apps und -Plugins
docs/       Anwenderdokumentation
```

### Code-Regeln

- Nullable Reference Types aktiv, Warnungen als Fehler.
- Öffentliche API klein halten; alles andere `internal`.
- Lesbarkeit vor Cleverness: verständliche Namen, wenige Abstraktionsebenen.
- Keine Abhängigkeiten außer `Microsoft.Extensions.*` ohne ausdrückliche Entscheidung.

### Arbeitsweise

- Kleine Schritte: jede Funktion mit Tests, Doku und Beispiel als ein Paket.
- Erst den Plugin-Vertrag festziehen, dann Extensions und Handler-Pipeline, dann Laden aus Verzeichnis.
- Abweichungen von dieser Spezifikation im Entscheidungslog unten nachtragen.

### Tests

**Entschieden:** xUnit v3 auf der Microsoft Testing Platform, Paket `xunit.v3` in Version 4.x (seit August 2026 final). Assertions mit dem eingebauten `Assert`, bei Bedarf Shouldly; kein FluentAssertions (seit v8 kostenpflichtig für kommerzielle Nutzung).

1. Getrennte Testprojekte: `Plugwright.Tests` für Unit-Tests, `Plugwright.IntegrationTests` für echtes Laden, Signieren und Prüfen.
2. Test-Plugins als eigene Projekte unter `tests/TestPlugins/`. Sie werden nicht referenziert, sondern gebaut und in den Testordner kopiert, wie echte Plugins.
3. Vorbereitung von Test-Plugins, NuGet-Paketen und signierten Manifesten einmal pro Lauf über eine Assembly-Fixture.
4. Jeder Integrationstest arbeitet in einem eigenen temporären Verzeichnis.
5. Tests, die Ladekontexte betreffen, laufen gezielt seriell über xUnit-Collections.
6. Szenarien mit Neustart und Exit-Code laufen in einem separaten Prozess, z. B. über eine kleine Test-Host-App.

## Offene Fragen und Entscheidungslog

### Offene Fragen für die nächsten Sessions

- [x] Architekturvariante festlegen: A, C oder A + C
- [x] Ziel-Framework bestätigen: nur .NET 10 oder zusätzlich ältere Versionen
- [x] Konfiguration je Plugin
- [x] Projektname und Lizenz
- [x] Doku-Werkzeug und Doku-Sprache
- [x] Testframework
- [x] Offene Punkte vor Start der Umsetzung (CI, Paketschnitt, Phase-1-Logging, Plugin-Instanzen, Ordnerkonvention, Ladeweg u. a.)
- [x] Doppelte Plugin-IDs
- [x] Versionssemantik bei DLL-Plugins
- [x] Direkt hinzugefügte Plugins bei registrierten Prüfern
- [ ] NuGet-Ordner: Drittpaket-Abhängigkeiten, Entpack-Cache und Aufräumen
- [x] Zertifikate: Zeitstempel, Sperrlistenprüfung
- [x] CLI: Befehls- und Paketname
- [ ] Scope v1 und v2 (Aktivieren/Deaktivieren) bestätigen

### Entscheidungslog

| Datum | Entscheidung | Begründung |
| --- | --- | --- |
| 2026-10-06 | Basis sind `IHost` und `IHostApplicationBuilder` | Moderne Standard-Abstraktionen von Microsoft nutzen |
| 2026-10-06 | Start mit Konsolen-App auf dem Generic Host | Kleiner, klarer Einstieg |
| 2026-10-06 | Veröffentlichung als Open Source auf GitHub | Daraus folgt der hohe Anspruch an die Doku |
| 2026-10-06 | Ziel-Framework ist ausschließlich .NET 10 | LTS, keine Altnutzer, C# 14 Extension Members verfügbar; Multi-Targeting bei Bedarf später |
| 2026-10-06 | Fehler in Configure: Fail-fast als Standard, optional überspringen und loggen | Stilles Überspringen verdeckt Fehler im Betrieb; Option für Szenarien, die nicht blockieren dürfen |
| 2026-10-06 | Nur Extension Members (Variante C), keine eigene Builder- oder App-Klasse | Funktioniert mit jedem Host-Typ, auch Web; Wrapper hätten je Host-Typ nachgebaut werden müssen |
| 2026-10-06 | Zwei explizite Aufrufe für alle Apps: builder.ApplyPlugins() vor, app.UsePlugins() nach Build() | Einheitlich für Konsole und Web; Nutzer steuert Position in der Middleware-Pipeline; .NET-Konvention AddX/UseX |
| 2026-10-06 | Plugins.Add…() speichert nur Typen; Handler-Pipeline verarbeitet sie | Neue Plugin-Arten (Web, Blazor, eigene) ohne Änderung am Kern |
| 2026-10-06 | Handler werden getrennt von Plugins direkt am Builder registriert | Klare Trennung: Plugins beschreiben was, Handler wie |
| 2026-10-06 | Alle passenden Handler laufen, in Registrierungsreihenfolge | Handler als Bausteine je Interface kombinierbar, keine Sammel-Handler nötig |
| 2026-10-06 | Kein Handler für einen Typ: Exception, Option überspringen und loggen gilt mit | Fast immer ein Konfigurationsfehler; konsistent mit Fail-fast |
| 2026-10-06 | Handler-Registrierung über builder.PluginHandlers.Add\<T>(); in v1 nur aus der Host-Anwendung | „Handlers“ ist in .NET mehrfach belegt; Handler aus Plugins machen das Verhalten unvorhersagbar |
| 2026-10-06 | Begriff „Plugin“ statt „Modul“ | „Modul“ steht in .NET für Aufteilung der eigenen App; „Plugin“ für optionale, nachladbare Erweiterungen; gängiger Suchbegriff |
| 2026-10-06 | v1-Quellen: direkt in Program.cs, DLL-Verzeichnis, NuGet-Ordner; DLL und NuGet mit Integritätsprüfung | Vorgabe für v1 |
| 2026-10-06 | Plugin-Metadaten und Abhängigkeiten zwischen Plugins kommen in v1 | Vorgabe für v1 |
| 2026-10-06 | Metadaten aus .nuspec bzw. Assembly, Code-Attribute als Fallback; Identität auf Ebene Assembly/Paket | Keine doppelte Pflege; entspricht der Art, wie Plugins ausgeliefert werden |
| 2026-10-06 | Abhängigkeit = Vorhandensein, Reihenfolge, Typen teilen; optionale Abhängigkeiten erst v2 | Ohne Typen teilen wären Abhängigkeiten bei isolierten Plugins kaum nutzbar; optionale erfordern Contract-Konzept |
| 2026-10-06 | NuGet.Packaging in eigenem Paket \*.NuGet | Versionsbereiche, TFM-Auswahl und Signaturen nicht selbst nachbauen; Kern bleibt schlank |
| 2026-10-06 | Attribute ergänzen Paket-/Assembly-Metadaten nur, überschreiben nie | Eindeutige Regel, nichts wird stillschweigend verworfen |
| 2026-10-06 | Integritätsprüfung in v1 per Hash oder Zertifikat; DLLs über signiertes Manifest, NuGet über Paketsignatur | Plattformunabhängig; Zertifikat erspart Listenpflege bei Updates |
| 2026-10-06 | Ohne Prüfkonfiguration: Development Warnung, sonst Exception | Bequem in der Entwicklung, sicher im Betrieb |
| 2026-10-06 | System.Security.Cryptography.Pkcs als Abhängigkeit erlaubt | Nötig für CMS-signierte Manifeste; Microsoft-Paket |
| 2026-10-06 | Integrität erweitert: Manifest bindet ID/Version, keine fremden Dateien; NuGet-Cache wird geprüft; Vertrauen auch über Aussteller; Algorithmus-Kennung | Schließt Manipulationswege; Zertifikatserneuerung und Algorithmuswechsel ohne Bruch |
| 2026-10-06 | IPluginVerifier, Prüf-Protokoll mit Event-IDs und API, CLI-Tool mit verify | Erweiterbar wie Handler; nachvollziehbar im Betrieb; prüfbar in CI |
| 2026-10-06 | Doku auf Englisch, ausschließlich mit DocFX | Geprüfte API-Querverweise, Codebeispiele aus echtem Code, nur .NET-Toolchain; Design-Flexibilität bewusst nachrangig |
| 2026-10-06 | Zunächst keine Versionsumschaltung; aktuelle Doku mit Versionshinweisen und Migrations-Guides | Mehr Aufwand als Nutzen, solange keine Hauptversionen parallel gepflegt werden |
| 2026-10-06 | Doku nach Diátaxis gegliedert, gehostet auf GitHub Pages | Klare Trennung nach Leserbedarf; kostenloses Hosting direkt am Repo |
| 2026-10-06 | Lizenz MIT | Verbreitet im .NET-Ökosystem, keine Hürden für Nutzer |
| 2026-10-06 | Projektname Plugwright | Eindeutig auffindbar, Plugin-Bezug im Namen; ExtendR, Graft und Tenon sind anderweitig belegt |
| 2026-10-06 | Testframework xUnit v3, getrennte Unit- und Integrationstests mit eigenen Test-Plugin-Projekten | Assembly-Fixtures für teure Vorbereitung, Standard im .NET-Open-Source-Umfeld |
| 2026-10-06 | Prüfer werden explizit über builder.PluginVerifiers.Add\<T>() registriert, auch die mitgelieferten; ohne Prüfer keine Prüfung, nur Log-Hinweis. Ersetzt „Ohne Prüfkonfiguration: Development Warnung, sonst Exception“ | Anwender entscheidet selbst über Prüfungen; einheitliches Modell wie bei Handlern |
| 2026-10-06 | Mehrere Prüfer: alle zuständigen müssen bestehen | Strenger und vorhersehbar; der schwächste Prüfer kann nichts aushebeln |
| 2026-10-06 | Konfiguration je Plugin in v1, in eigener pluginsettings.json unter Plugwright:Plugins:{Id}; Datei gewinnt bei Konflikten | appsettings.json ist Teil der Auslieferung und nicht von der App speicherbar; Grundlage für Aktivieren in v2 |
| 2026-10-06 | IPlugin.Configure(PluginContext) statt Configure(IHostApplicationBuilder) | Kontextobjekt ist erweiterbar, ohne IPlugin nach 1.0 zu brechen |
| 2026-10-06 | Prüfer-Einstellungen im Code, optional ausdrücklich an einen appsettings-Abschnitt gebunden; pluginsettings.json darf sie nie setzen | Einstellungen sind die Vertrauensbasis; die von der App beschreibbare Datei darf sie nicht aushebeln |
| 2026-10-07 | CI mit GitHub Actions ab Start: Build, Tests, DocFX-Build; NuGet-Publish später | Codebeispiele in der Doku müssen im CI mitkompiliert werden |
| 2026-10-07 | Paketschnitt bestätigt; IPluginHandler, PluginDescriptor und IPluginVerifier im Hauptpaket | Handler und Prüfer kommen nur aus der Host-Anwendung; Abstractions bleibt minimal |
| 2026-10-07 | Doppelter Aufruf von ApplyPlugins() oder UsePlugins() wirft eine Exception | Fast immer ein Fehler; konsistent mit Fail-fast |
| 2026-10-07 | Log-Einträge aus Phase 1 werden gepuffert und in UsePlugins() ausgegeben; bei Fail-fast trägt die Exception die Informationen | Vor Build() gibt es keinen ILoggerFactory; eine temporäre Factory würde Provider doppelt aufbauen |
| 2026-10-07 | PluginContext enthält zusätzlich einen (gepufferten) ILogger | Plugins sollen beim Einrichten ohne Umwege loggen können |
| 2026-10-07 | Genau eine Plugin-Instanz je Typ, gehalten in PluginDescriptor.Instance, genutzt von allen Handlern in beiden Phasen | Kein doppelter Zustand; Phase 2 braucht Zugriff auf die Instanz aus Phase 1 |
| 2026-10-07 | Plugin-Klassen einer Assembly teilen sich den Settings-Abschnitt | Folgt aus der Identität auf Ebene der Assembly |
| 2026-10-07 | „Überspringen und loggen“ als ein globaler Schalter für Configure-Fehler, fehlende Handler und fehlgeschlagene Prüfung | Kleine API; Unterscheidung nach Fehlerart bei Bedarf später |
| 2026-10-07 | Übersprungene Plugins nehmen alle (transitiv) abhängigen Plugins mit | Abhängige würden sonst an fehlenden Typen scheitern |
| 2026-10-07 | DLL-Verzeichnis: plugins/<Name>/<Name>.dll, Ordner ist Einheit für Hashes, Manifest und Ladekontext | Eindeutige Haupt-Assembly ohne Heuristik |
| 2026-10-07 | System.Reflection.MetadataLoadContext als Abhängigkeit erlaubt | Metadaten ohne Code-Ausführung mit handhabbarer Reflection-API |
| 2026-10-07 | Ladeweg: ohne Prüfer LoadFromAssemblyPath, mit Prüfern LoadFromStream; native Bibliotheken nach Hash-Prüfung vom Pfad | Kompatibel ohne Prüfung, kein Austausch zwischen Prüfen und Laden mit Prüfung; Restrisiko bei nativen Bibliotheken dokumentiert |
| 2026-10-07 | xUnit v3 in Version 4.x (Paket xunit.v3 4.0.1) statt 3.x | 4.x ist seit August 2026 final; die Spezifikation sah den Umstieg dann vor |
| 2026-10-07 | Derselbe Plugin- oder Handler-Typ zweimal hinzugefügt: ArgumentException | Fast immer ein Fehler; betrifft nicht die offene Frage doppelter Plugin-IDs |
| 2026-10-07 | Fehler in IPluginHandler.Use (Phase 2) werfen immer eine PluginException, unabhängig von FailureMode | „Überspringen und loggen“ ist nur für Phase 1 festgelegt; nach Phase 1 lässt sich ein Plugin nicht mehr sauber entfernen |
| 2026-10-07 | Fehlermeldungen als PluginException mit PluginId; Fehlbedienung der Phasen als InvalidOperationException | Gezielt abfangbar; Standard-Exception für falsche Aufrufreihenfolge |
| 2026-10-07 | PluginDescriptor.Instance bleibt object; zusätzlich Basisklasse PluginHandler<TPlugin> mit typisierter Instanz | Plugins eigener Art sollen IPlugin nicht implementieren müssen; die Basisklasse erspart Casts und CanHandle im häufigsten Fall |
| 2026-10-07 | Relativer Pfad in AddFromDirectory bezieht sich auf ContentRootPath | Gleiche Basis wie appsettings.json; Konvention des Generic Host |
| 2026-10-07 | Fehlendes Plugin-Verzeichnis: Warnung im Log, keine Exception | Eine Neuinstallation ohne Plugins soll starten; Tippfehler sind im Log sichtbar |
| 2026-10-07 | Doppelte Plugin-IDs: Exception mit beiden Quellen, auch bei SkipAndLog; Vergleich ohne Groß-/Kleinschreibung | Unklar, welche Quelle gewinnen soll; Konfigurationsschlüssel unterscheiden nicht nach Groß-/Kleinschreibung |
| 2026-10-07 | Defekter Plugin-Ordner (fehlende oder nicht ladbare Haupt-Assembly, kein Plugin-Typ) ist ein Plugin-Fehler | Ein Unterordner ist eine bewusste Installation; sein Ausfall darf nicht still bleiben |
| 2026-10-07 | Gemeinsame Assemblies kommen in der Version des Hosts; fehlt sie im Host, lädt das Plugin seine Kopie | Typgleichheit für Plugin-Vertrag und DI; Plugins dürfen zusätzliche Microsoft.Extensions-Pakete nutzen |
| 2026-10-07 | Plugin-Typen im Verzeichnis: nur öffentliche Typen der Haupt-Assembly | Klare Regel ohne Reflection auf interne Typen |
| 2026-10-07 | PluginCollection ist nicht mehr aufzählbar; Quelle des Plugins steht in Fehlermeldungen und Log-Events (PluginSource) | Mit Verzeichnissen wären nur die Typen aus dem Code aufzählbar; im Betrieb muss erkennbar sein, woher ein Plugin kommt |
| 2026-10-07 | PluginCollection zählt die hinzugefügten Quellen auf (öffentlich: PluginSource, PluginTypeSource, PluginDirectorySource). Ersetzt „PluginCollection ist nicht mehr aufzählbar“ | Iterierbar und ehrlich: enthält, was hinzugefügt wurde. Verzeichnisse werden nicht schon beim Hinzufügen gescannt, da Handler, Prüfer und Fehlermodus erst bei ApplyPlugins() feststehen und ungeprüfte DLLs sonst geladen würden. Die Quellen-Typen braucht später auch IPluginVerifier.CanVerify |
| 2026-10-07 | Plugin-Version ist SemVer aus <Version> (InformationalVersion ohne Build-Metadaten); Assembly-Referenzen prüfen zusätzlich die AssemblyVersion als Minimum | Gleiches Format wie NuGet-Pakete später; Referenzprüfung folgt der .NET-Bindungsregel |
| 2026-10-07 | [PluginDependency] nur mit MinVersion | Reicht für DLL-Plugins; NuGet-Pakete bringen Versionsbereiche aus der .nuspec mit; kein eigener Bereichsparser im Kern |
| 2026-10-07 | [Plugin] und [PluginDependency] nur auf Assembly-Ebene | Metadaten und Abhängigkeiten gehören eindeutig zur Assembly |
| 2026-10-07 | Fehlende oder zu alte Abhängigkeit, Zyklus: Plugin-Fehler; mit SkipAndLog werden betroffene Plugins (bei Zyklen alle Mitglieder) samt Abhängigen übersprungen | Konsistent mit Fail-fast; andere Plugins sollen weiterlaufen können |
| 2026-10-07 | Schlägt ein Plugin-Typ einer Assembly fehl, gelten abhängige Plugins als nicht erfüllt; andere Typen derselben Assembly laufen weiter | Abhängige brauchen evtl. genau die Dienste des fehlgeschlagenen Typs; Plugins im Host-Projekt sollen sich nicht gegenseitig blockieren |
| 2026-10-07 | Plugins aus dem Code können nicht von Plugins aus einem Verzeichnis abhängen | Sie liegen im Standard-Kontext und würden eine eigene Kopie der Assembly laden; Typgleichheit wäre nicht möglich |
| 2026-10-07 | Metadaten von Plugin-Ordnern per MetadataLoadContext mit den Host-Assemblies (TPA) und den Dateien des Ordners | Abhängigkeiten werden geprüft, bevor Plugin-Code geladen wird |
| 2026-10-07 | InternalsVisibleTo für Plugwright.Tests | Unit-Tests für SemVer-Vergleich und Abhängigkeitsauflösung ohne öffentliche API dafür |
| 2026-10-07 | Mitgelieferte Prüfer über eigene Methoden (AddHashVerifier, AddCertificateVerifier) mit Delegate oder Konfigurationsabschnitt; eigene über Add(instance)/Add<T>() | Prüfer brauchen Einstellungen; auffindbar und typgeprüft, Bindung an Konfiguration bleibt ein ausdrücklicher Aufruf |
| 2026-10-07 | Plugins aus dem Code werden nicht geprüft | Sie sind Teil der Host-Anwendung; sonst bräuchte jede App einen Prüfer für den eigenen Code |
| 2026-10-07 | Keine Zeitstempel für Manifest-Signaturen in v1 | Deutlich weniger Aufwand; nach Zertifikatsablauf wird neu signiert |
| 2026-10-07 | Sperrlistenprüfung einstellbar, Standard NoCheck | Rechner ohne Netzzugang sollen starten; die Doku empfiehlt Online beim Vertrauen über Aussteller |
| 2026-10-07 | CLI Plugwright.Cli / plugwright; lock-Befehl vor die übrigen Schritte gezogen | Doku und Beispiele zur Prüfung brauchen ein echtes Werkzeug für die Lock-Datei |
| 2026-10-07 | Prüffehler aller Art sind Plugin-Fehler und lassen sich mit SkipAndLog überspringen | Konsistent mit den übrigen Ladefehlern |
| 2026-10-07 | PluginReport im DI-Container mit aktiven und übersprungenen Plugins und Prüfergebnissen | Erfüllt „Ergebnisse per API“ und die früher besprochene Abfrage geladener Plugins |
| 2026-10-07 | Hinweis „keine Prüfer registriert“ auf Ebene Information, nur wenn Plugin-Verzeichnisse hinzugefügt wurden | Ohne Verzeichnisse gibt es nichts zu prüfen; Information statt Warnung, da ungeprüftes Laden eine zulässige Wahl ist |
