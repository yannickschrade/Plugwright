# Spike R-51: Statische Web-Assets dynamisch geladener Blazor-Plugins

Stand 2026-10-11. Ziel laut Entscheidungslog: volle Gleichwertigkeit mit Projektreferenzen (`MapStaticAssets`, Fingerprinting, Kompression, `@Assets`, `ImportMap`, Scoped CSS). Getestet mit einer Razor Class Library (`wwwroot/plugin.css`, `wwwroot/logo.svg`, `Hello.razor.css`, `Hello.razor.js`) und einem Blazor-Host (.NET 10, `dotnet new blazor --interactivity Server`), Plugin nicht referenziert.

## Was die Plugin-Formate enthalten

| Format | Endpunkte | Dateien | Kompression | Scoped CSS |
| --- | --- | --- | --- | --- |
| `dotnet build` | `{Id}.staticwebassets.endpoints.json` (Build) | **nicht** in der Ausgabe, nur über `staticwebassets.runtime.json` im Quellordner | nein | nur in `obj/` |
| `dotnet publish` | `{Id}.staticwebassets.endpoints.json` (Publish), Routen **ohne** `_content/{Id}/` | `wwwroot/` | `.br` und `.gz` | `wwwroot/{Id}.styles.css` |
| `dotnet pack` | MSBuild-Items in `build/Microsoft.AspNetCore.StaticWebAssetEndpoints.props`, Routen **mit** `_content/{Id}/` | `staticwebassets/` | nein | `staticwebassets/{Id}.{fingerprint}.bundle.scp.css` |

Alle Formate mit Endpunkten enthalten Fingerprints, SRI-Hashes, ETags und Cache-Header.

## Was funktioniert

1. **`MapStaticAssets(manifestPath)` liest die Dateien über `IWebHostEnvironment.WebRootFileProvider`**, nicht relativ zum Manifest. Ohne passenden File-Provider liefert eine gemappte Route 200 mit leerem Inhalt.
2. **Umschreiben + File-Provider**: Route, `AssetFile` und Eigenschaft `label` mit `_content/{Id}/` präfixen, `wwwroot` des Plugins über einen Präfix-File-Provider in einen `CompositeFileProvider` vor `WebRootFileProvider` hängen. Ergebnis: Inhalt, `Content-Encoding: br`, ETag, Fingerprint-Route mit `max-age=31536000, immutable`, Scoped CSS und JS-Modul werden korrekt ausgeliefert.
3. **Aus dem Speicher** (eigener `IFileProvider`, `PhysicalPath = null`): alles wie bei Dateien von der Platte, auch `Range` (206) und `If-None-Match` (304).
4. **`@Assets` und `ImportMap`**: `WithStaticAssets(pluginManifest)` **ersetzt** die Sammlung des Hosts (Host-Dateien verlieren ihre Fingerprints). Mehrere Manifeste werden nicht zusammengeführt. Funktioniert: **ein zusammengeführtes Manifest** (Host + alle Plugins), einmal `MapStaticAssets(merged)` und `WithStaticAssets(merged)`. Dann haben Host- und Plugin-Dateien Fingerprints, `ImportMap` enthält die JS-Module der Plugins, `_framework/blazor.web.js` funktioniert weiter.
5. **Development** (Host aus `bin/Debug`, Build-Manifest des Hosts): zusammengeführtes Manifest funktioniert ebenso.
6. Ohne `WithStaticAssets` liefert `@Assets["_content/{Id}/x.css"]` den Pfad ohne Fingerprint; die Datei wird trotzdem ausgeliefert (ETag, `must-revalidate`), nur ohne Langzeit-Cache.

## Was zu beachten ist

- `app.MapStaticAssets()` des Hosts und ein zusammengeführtes Manifest dürfen nicht beide gemappt werden (doppelte Routen). Für volle Gleichwertigkeit muss die App ihren `MapStaticAssets()`-Aufruf ersetzen.
- `MapStaticAssets` nimmt nur einen Dateipfad: das zusammengeführte Manifest muss als Datei existieren (z. B. im Temp-Ordner des Prozesses wie native Bibliotheken).
- Verzeichnis-Plugins brauchen **Publish-Ausgabe**; Build-Ausgabe enthält die Dateien nicht.
- Pakete: Endpunkte aus MSBuild-XML lesen und nach JSON übersetzen; keine vorkomprimierten Varianten.
- Scoped CSS: Plugins aus Verzeichnissen/Paketen sind nicht in `{App}.styles.css` enthalten; ihr Bündel muss im Layout per `<link>` eingebunden werden. Bei Projektreferenzen ist es schon enthalten und darf nicht doppelt eingebunden werden.
- Integrität: Manifest und `wwwroot` sind Teil der Plugin-Dateien und damit von Lock-Datei bzw. Manifest abgedeckt. Aus dem Speicher ausgeliefert, gelten die geprüften Bytes; von der Platte könnten Dateien nach der Prüfung getauscht werden.
