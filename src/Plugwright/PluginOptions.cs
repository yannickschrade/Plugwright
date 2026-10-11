namespace Plugwright;

/// <summary>
/// Options for the plugin system, available as <c>builder.Plugins.Options</c>.
/// </summary>
public sealed class PluginOptions
{
    /// <summary>
    /// Gets or sets what happens when a plugin cannot be loaded or configured.
    /// The default is <see cref="PluginFailureMode.FailFast"/>.
    /// </summary>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
    /// </code>
    /// </example>
    public PluginFailureMode FailureMode { get; set; } = PluginFailureMode.FailFast;

    /// <summary>
    /// Gets or sets the path of the application-managed plugin settings file. A relative path is resolved against the
    /// content root. The default is <c>pluginsettings.json</c>; <see langword="null"/> turns the file off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>builder.ApplyPlugins()</c> adds the file as the last configuration source, so its values win over all others,
    /// including environment variables and command-line arguments. Only keys under <c>Plugwright:Plugins</c> are read
    /// from it, so it can change plugin settings and turn plugins on and off, but nothing else. Changes to the file are
    /// picked up while the application runs; plugins that bind their settings with <c>IOptionsMonitor</c> see them.
    /// Turning a plugin on or off takes effect at the next start.
    /// </para>
    /// <para>
    /// The application writes the file with <see cref="IPluginSettingsStore"/>. Choose a writable location, for example
    /// below <c>/var/lib</c> on Linux or <c>%ProgramData%</c> on Windows. The file need not exist.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.Options.SettingsFile = "/var/lib/myapp/pluginsettings.json";
    /// </code>
    /// </example>
    public string? SettingsFile { get; set; } = "pluginsettings.json";

    /// <summary>
    /// Gets or sets how <see cref="IApplicationRestarter"/> restarts the application. The default is
    /// <see cref="ApplicationRestartMode.ExitCode"/>.
    /// </summary>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.Options.RestartMode = ApplicationRestartMode.Relaunch;
    /// </code>
    /// </example>
    public ApplicationRestartMode RestartMode { get; set; } = ApplicationRestartMode.ExitCode;

    /// <summary>
    /// Gets or sets the exit code with which the application stops for a restart in
    /// <see cref="ApplicationRestartMode.ExitCode"/> mode. The default is 75 (<c>EX_TEMPFAIL</c>: a temporary failure,
    /// try again).
    /// </summary>
    /// <remarks>
    /// The code is not 0, so that service managers that restart only after a failure restart the application as well:
    /// systemd with <c>Restart=on-failure</c>, Windows services with recovery actions for stops with errors, and Docker
    /// with <c>--restart on-failure</c>. The code is set with <see cref="Environment.ExitCode"/>, so it only applies if
    /// <c>Main</c> returns <c>void</c> or <see cref="Task"/>, not an <see cref="int"/> of its own.
    /// </remarks>
    public int RestartExitCode { get; set; } = 75;
}
