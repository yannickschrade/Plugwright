# Configure plugins from appsettings.json

Plugin sources, the failure mode and plugin verification can come from configuration instead of code. Operators can then change which plugins are loaded, and how they are verified, without rebuilding the application.

The complete, runnable project is in `samples/ConfiguredPlugins` in the repository.

## Let the configuration decide

Read the `Plugwright` section with two calls:

[!code-csharp[](../../samples/ConfiguredPlugins/App/Program.cs?name=host)]

- `builder.Plugins.AddFromConfiguration` reads the failure mode and the plugin sources.
- `builder.PluginVerifiers.AddFromConfiguration` turns on the verifiers the configuration names, or none.
- `builder.Plugins.AddNuGetSupport()` comes from Plugwright.NuGet. Call it first if the configuration contains package folders or the package signature verifier.

Both calls are explicit: without them, the configuration has no say. You can combine them with calls in code, for example plugins added with `Add<T>()`.

## Write the configuration

[!code-json[](../../samples/ConfiguredPlugins/App/appsettings.json)]

| Key | Meaning |
| --- | --- |
| `FailureMode` | `FailFast` (default) or `SkipAndLog`, see [Keep running when a plugin fails](handle-plugin-failures.md). |
| `Directories` | Plugin directories, see [Load plugins from a directory](load-plugins-from-a-directory.md). |
| `Packages` | Package folders: a path, or an object with `Path`, `Feed`, `DownloadDirectory`, `DownloadTimeout` and `VerifyDownloads`. See [Load plugins from NuGet packages](load-plugins-from-packages.md). |
| `Verification` | The verifiers, see below. |
| `Plugins` | The plugins' own settings, as always. |

Relative paths are resolved against the content root. The sample sets the content root to the application's folder, so that the paths in `appsettings.json` point there no matter where the application is started.

Unknown keys are errors, also inside the verifier settings. A typo such as `Verfication` would otherwise silently turn verification off.

## Choose the verifiers

Every key in `Verification` turns on one verifier, with the settings of its own registration method:

| Key | Verifier | Settings |
| --- | --- | --- |
| `Hash` | <xref:Plugwright.HashVerifier> | <xref:Plugwright.HashVerifierOptions> |
| `Certificate` | <xref:Plugwright.CertificateVerifier> | <xref:Plugwright.CertificateVerifierOptions> |
| `PackageSignature` | <xref:Plugwright.PackageSignatureVerifier> (Plugwright.NuGet) | <xref:Plugwright.CertificateVerifierOptions> |

A verifier is on when its section has settings, or is `true`. `"Hash": true` turns on the hash verifier with its default lock file, `plugins.lock.json` in the content root; an empty object `"Hash": {}` does not work, because .NET configuration ignores it. `false` turns a verifier off, which is useful in environment-specific files or environment variables such as `Plugwright__Verification__Hash=false`.

Trusted certificate authorities for signed plugins:

```json
{
  "Plugwright": {
    "Verification": {
      "Certificate": {
        "TrustedPublishers": [
          { "IssuerCertificate": "/etc/myapp/acme-ca.crt", "Subject": "CN=Acme Plugins, O=Acme" }
        ],
        "RevocationMode": "Online"
      }
    }
  }
}
```

Without any verifier in the configuration, plugins are not verified, and a notice is logged at startup.

## Verify downloads, or not

Packages downloaded from a feed are verified like all others by default. If you trust the feed itself, for example a feed of your own company, you can turn that off for one package folder:

```json
{
  "Plugwright": {
    "Packages": [
      { "Path": "packages", "Feed": "https://nuget.example.com/v3/index.json", "VerifyDownloads": false }
    ]
  }
}
```

Packages in the package folder itself are still verified.

## Protect the configuration

> [!IMPORTANT]
> Whoever can change this configuration decides which plugins run and whether they are verified. Protect `appsettings.json`, environment-specific files and environment variables of the application from whoever can write to the plugin directory. The application-managed plugin settings can never change these keys.

## See also

- [Configuration reference](../reference/configuration.md)
- [Security model](../explanation/security-model.md)
