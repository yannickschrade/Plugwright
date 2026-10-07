# Verify plugins with a lock file

The <xref:Plugwright.HashVerifier> only loads plugins whose files match a lock file on the host side. A changed, added or removed file in a plugin folder stops the plugin from loading. This guide shows how to set it up and keep it up to date.

The complete, runnable project is in `samples/GettingStarted` in the repository.

## Register the verifier

Add the verifier before calling `ApplyPlugins()`:

[!code-csharp[](../../samples/GettingStarted/App/Program.cs?name=host)]

Without the `configure` delegate, the lock file is `plugins.lock.json` in the content root. A relative path is resolved against the content root as well.

To take the settings from configuration instead of code, bind them explicitly:

```csharp
builder.PluginVerifiers.AddHashVerifier(builder.Configuration.GetSection("Plugwright:Verification:Hash"));
```

```json
{
  "Plugwright": {
    "Verification": {
      "Hash": { "LockFile": "/etc/myapp/plugins.lock.json" }
    }
  }
}
```

Which verifiers exist is always decided in code; configuration can only change their settings.

## Create the lock file

Create the lock file with the [`plugwright` tool](../reference/cli.md) after installing or updating plugins:

```bash
plugwright lock /srv/myapp/plugins --output /etc/myapp/plugins.lock.json
```

The lock file covers every file in every plugin folder, not only the main assembly. It looks like this:

```json
{
  "version": 1,
  "plugins": {
    "Acme.Hello": {
      "files": {
        "Acme.Hello.deps.json": "sha256-4Wk0JBC2mP5ka5Ih+Mt8aEMfxgV/3ZxlxCtJdGkmQ9U=",
        "Acme.Hello.dll": "sha256-q1MKE+RZFJgrefm34/uplM/R8/si9xzqGvvwK0YMbR0="
      }
    }
  }
}
```

Hashes carry the algorithm as a prefix, so that the algorithm can change in a later version.

## Protect the lock file

The lock file is what you trust. Verification only helps if whoever can write to the plugin directory cannot also change the lock file:

- Store the lock file outside the plugin directory, in a location only administrators or the deployment can write to, for example `/etc/myapp` on Linux or `%ProgramData%\MyApp` with restricted permissions on Windows.
- If the lock file path comes from `appsettings.json`, protect that file in the same way.

## What fails, and when

A plugin fails verification when:

- the lock file has no entry for its folder,
- a file in the folder is not listed in the lock file,
- a listed file is missing, or
- a file does not match its hash.

Verification runs before Plugwright reads anything from the plugin's assemblies. A plugin that fails is never loaded. By default, the application stops with a <xref:Plugwright.PluginException>; with [`SkipAndLog`](handle-plugin-failures.md), the plugin and the plugins that depend on it are skipped.

A missing or invalid lock file fails every plugin from a directory.

## What changes when plugins are verified

Verified plugins are loaded from the bytes that were verified, not from the files. Nothing can be swapped between verification and loading. One consequence: <xref:System.Reflection.Assembly.Location> is empty for assemblies of verified plugins. Code that needs the path of its own assembly should use <xref:System.AppContext.BaseDirectory> of the host or a setting instead.

Native libraries can only be loaded from disk. Plugwright checks them against their verified content right before loading them.

Plugins added in code with `Add<T>()` are part of the host application and are not verified.

## Check the results

Every verification is logged with event ID 1005 (passed) or 1006 (failed). The results are also available in the <xref:Plugwright.PluginReport>, see [Keep running when a plugin fails](handle-plugin-failures.md#see-what-was-skipped).

## See also

- [Sign plugins and trust their publisher](sign-plugins.md): an alternative that needs no update on the host when plugins change.
- [Security model](../explanation/security-model.md)
- [CLI reference](../reference/cli.md)
