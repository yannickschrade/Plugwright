# Security model

Plugins run with full trust inside your process. A plugin can do everything your application can do: read every configuration value, call every service, access files and the network with the rights of the process. Plugwright does not sandbox plugins, and .NET offers no way to do so within one process.

The question is therefore not what a plugin may do, but which plugins get loaded at all. That is what verification is for.

## What verification protects against

Verification is optional. When you register a verifier, Plugwright loads a plugin from a directory only if every responsible verifier accepts it:

- The <xref:Plugwright.HashVerifier> accepts exactly the files listed in a lock file. Changed, added and removed files are rejected.

This protects against plugin files that were changed or added without going through your deployment: a tampered DLL, an extra assembly dropped into a plugin folder, a plugin copied in by someone who may write to the plugin directory but should not decide what runs.

## The trust anchor must be protected

Verification moves the trust from the plugin directory to the lock file and the verifier settings. If the same person can change both the plugin directory and the lock file, verification protects nothing.

- Store the lock file where whoever can write to the plugin directory cannot write.
- Verifier settings come from code by default. If you bind them to `appsettings.json`, protect that file in the same way.

## No gap between verification and loading

Plugwright reads all files of a plugin into memory, verifies these bytes, reads the plugin's metadata from them and loads its assemblies from them. Replacing a file on disk after verification has no effect.

Native libraries are the exception, because the operating system loads them from disk. Plugwright compares them with their verified content right before loading them. A very short window remains between that check and the load.

## What verification does not do

- It does not judge what a plugin does. A verified plugin is a plugin you approved, not a safe one.
- It does not verify plugins added in code with `Add<T>()`. They are part of your application.
- It does not use Authenticode or strong-name signatures. Microsoft does not consider strong names a security feature.
