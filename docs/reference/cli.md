# Command-line tool

The `plugwright` tool supports deploying verified plugins. Install it as a .NET tool:

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
| `<plugin-directory>` | The plugin directory, with one subfolder per plugin. |
| `-o`, `--output <file>` | The lock file to write. Default: `plugins.lock.json` in the current directory. |
| `-h`, `--help` | Show help. |

The lock file contains the sha256 hash of every file in every subfolder. Files directly in the plugin directory are not part of any plugin; the tool lists them as warnings and leaves them out.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. |
| 1 | The command failed, for example because the plugin directory does not exist. |
| 2 | Invalid arguments. |
