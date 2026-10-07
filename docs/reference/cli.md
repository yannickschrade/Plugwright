# Command-line tool

The `plugwright` tool creates what the built-in verifiers check: lock files and signed manifests. Install it as a .NET tool:

```bash
dotnet tool install --global Plugwright.Cli
```

## plugwright lock

Creates the lock file for the <xref:Plugwright.HashVerifier>.

```text
plugwright lock <plugin-directory> [--output <file>]
```

| Argument or option | Meaning |
| --- | --- |
| `<plugin-directory>` | The plugin directory with one subfolder per plugin, or a folder of NuGet packages. |
| `-o`, `--output <file>` | The lock file to write. Default: `plugins.lock.json` in the current directory. |
| `-h`, `--help` | Show help. |

The lock file contains the sha256 hash of every file in every subfolder, under the folder's name, and of every `.nupkg` file, under its file name without `.nupkg`. Other files directly in the directory are not part of any plugin; the tool lists them as warnings and leaves them out.

## plugwright sign

Signs a plugin folder for the <xref:Plugwright.CertificateVerifier>.

```text
plugwright sign <plugin-folder> --certificate <file> [--password <password>]
```

| Argument or option | Meaning |
| --- | --- |
| `<plugin-folder>` | The plugin folder, containing `<folder-name>.dll`. |
| `-c`, `--certificate <file>` | A PKCS#12 file (`.pfx`, `.p12`) with the signing certificate and its private key. Intermediate certificates in the file are embedded in the signature; self-signed root certificates are not. |
| `-p`, `--password <password>` | The password of the certificate file. Default: the environment variable `PLUGWRIGHT_CERTIFICATE_PASSWORD`. |
| `-h`, `--help` | Show help. |

The tool reads the plugin ID and version from the folder's main assembly, without running it, and writes `plugwright.manifest.p7s` with the hash of every other file in the folder. An existing manifest is replaced. The output names the signing certificate and its SHA-256 thumbprint.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. |
| 1 | The command failed, for example because the plugin directory does not exist. |
| 2 | Invalid arguments. |
