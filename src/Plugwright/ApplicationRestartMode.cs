namespace Plugwright;

/// <summary>
/// How <see cref="IApplicationRestarter"/> restarts the application. Set with <see cref="PluginOptions.RestartMode"/>.
/// </summary>
public enum ApplicationRestartMode
{
    /// <summary>
    /// Stop the application with <see cref="PluginOptions.RestartExitCode"/>, so that the service manager starts it
    /// again: systemd, Windows services, Docker, Kubernetes or IIS. Without a service manager, the application only stops.
    /// </summary>
    ExitCode,

    /// <summary>
    /// Start a new process of the application with the same executable and arguments, then stop. The new process waits
    /// until the old one has exited. For console and desktop applications without a service manager; do not use it
    /// under a service manager, which would not know the new process.
    /// </summary>
    Relaunch,
}
