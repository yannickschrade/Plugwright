# Security model

Plugins run with full trust inside your process. A plugin can do everything your application can do: read every configuration value, call every service, access files and the network with the rights of the process. Plugwright does not sandbox plugins, and .NET offers no way to do so within one process.

The question is therefore not what a plugin may do, but which plugins get loaded at all. That is what verification is for.

## What verification protects against

Verification is optional. When you register a verifier, Plugwright loads a plugin from a directory only if every responsible verifier accepts it:

- The [`HashVerifier`](../api/Plugwright.HashVerifier.md) accepts exactly the files listed in a lock file on the host. Changed, added and removed files are rejected.
- The [`CertificateVerifier`](../api/Plugwright.CertificateVerifier.md) accepts plugins whose files match a manifest signed by a trusted publisher. Changed, added and removed files are rejected as well, and so is a plugin signed by anybody else.
- For NuGet packages, the [`HashVerifier`](../api/Plugwright.HashVerifier.md) checks each package file against the lock file, and the [`PackageSignatureVerifier`](../api/Plugwright.NuGet.PackageSignatureVerifier.md) checks NuGet package signatures. Library packages are verified like plugin packages, because their code runs in the process as well.

This protects against plugin files that were changed or added without going through your deployment: a tampered DLL, an extra assembly dropped into a plugin folder, a plugin copied in by someone who may write to the plugin directory but should not decide what runs.

## The trust anchor must be protected

Verification moves the trust from the plugin directory to the lock file, the trusted certificates and the verifier settings. If the same person can change both the plugin directory and these, verification protects nothing.

- Store the lock file and trusted issuer certificates where whoever can write to the plugin directory cannot write.
- Keep the private key of a signing certificate with the publisher, never on the machines that run the plugins.
- Verifier settings come from code by default. If you bind them to `appsettings.json`, protect that file in the same way.
- If the application lets the configuration choose the verifiers (`PluginVerifiers.AddFromConfiguration`), whoever can change the configuration can also turn verification off. Protect the configuration files and environment variables of the application accordingly.

## What the plugin settings file can change

The application can write `pluginsettings.json` itself, for example from an administration page. Its values win over all other configuration, environment variables included. It is limited on purpose:

- It only contributes keys under `Plugwright:Plugins`: plugin settings and whether a plugin is on. It cannot change other settings, the plugin sources or the verifiers.
- It cannot turn off a required plugin. Which plugins are required is decided in code.
- Turning a plugin off never loads anything. A plugin that is off is neither verified nor loaded.

Still, whoever can write the file can turn optional plugins off and change their settings. Grant write access to the file accordingly.

## The catalog endpoint

`MapPluginCatalog()` publishes the IDs, versions and states of all plugins, and the reasons why plugins are not loaded. That tells an attacker which plugins and versions run. The endpoint is never mapped automatically; protect it when the application is reachable from outside. Error messages are only included on request.

## No gap between verification and loading

Plugwright reads all files of a plugin into memory, verifies these bytes, reads the plugin's metadata from them and loads its assemblies from them. Replacing a file on disk after verification has no effect.

Native libraries are the exception, because the operating system loads them from disk. Plugwright compares them with their verified content right before loading them. A very short window remains between that check and the load.

## Signatures and time

Manifest signatures carry no timestamp. A signing certificate must be valid when the plugin is verified, not only when it was signed. When it expires, plugins must be signed again. Revocation checking is off by default, so that machines without network access start; turn it on for publishers trusted through a certificate authority.

## Downloads

A package folder can download missing library packages from a feed. This is off by default. When it is on, the application accesses the network during startup and runs code that was not part of the deployment. Downloaded packages are verified like all other packages before they are stored or used, and the download directory needs the same protection as the package folder.

## What verification does not do

- It does not judge what a plugin does. A verified plugin is a plugin you approved, not a safe one.
- It does not verify plugins added in code with `Add<T>()`. They are part of your application.
- It does not use Authenticode or strong-name signatures. Microsoft does not consider strong names a security feature.
