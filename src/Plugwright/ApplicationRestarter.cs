using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Plugwright;

/// <summary>
/// The built-in <see cref="IApplicationRestarter"/>: stops the application with an exit code, or starts a new process
/// of it once the application has stopped.
/// </summary>
internal sealed class ApplicationRestarter(
    IHostApplicationLifetime lifetime, ILogger logger, ApplicationRestartMode mode, int exitCode) : IApplicationRestarter
{
    /// <summary>
    /// The environment variable that tells a relaunched process which process to wait for.
    /// </summary>
    public const string ParentProcessVariable = "PLUGWRIGHT_RESTART_PARENT_PID";

    /// <summary>
    /// How long a relaunched process waits for the previous one to exit.
    /// </summary>
    public static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    private int _requested;

    public void RequestRestart()
    {
        if (Interlocked.Exchange(ref _requested, 1) == 1)
        {
            return;
        }

        PluginLog.ApplicationRestartRequested(logger, mode);
        if (mode == ApplicationRestartMode.Relaunch)
        {
            // Start the new process only when the services have stopped, for example when the web server has released
            // its ports. The new process still waits until this one has exited.
            lifetime.ApplicationStopped.Register(StartNewProcess);
        }
        else
        {
            Environment.ExitCode = exitCode;
        }

        lifetime.StopApplication();
    }

    private static void StartNewProcess()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The application cannot restart itself: its executable is not known.");
        var arguments = Environment.GetCommandLineArgs();

        var startInfo = new ProcessStartInfo(processPath) { UseShellExecute = false, WorkingDirectory = Environment.CurrentDirectory };

        // With "dotnet app.dll", the first argument is the application's assembly, which dotnet needs again.
        // An apphost (app.exe, ./app) gets its own path as the first argument, which is not passed on.
        var isDotnetHost = Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        foreach (var argument in isDotnetHost ? arguments : arguments.Skip(1))
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[ParentProcessVariable] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        using var process = Process.Start(startInfo);
    }

    /// <summary>
    /// In a process that a restart started, waits until the previous process has exited. Called by
    /// <c>builder.ApplyPlugins()</c>, before any plugin is loaded.
    /// </summary>
    public static void WaitForPreviousProcess(ILogger logger)
    {
        var value = Environment.GetEnvironmentVariable(ParentProcessVariable);
        if (value is null)
        {
            return;
        }

        // Processes this one starts must not wait for it again.
        Environment.SetEnvironmentVariable(ParentProcessVariable, null);
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
        {
            return;
        }

        try
        {
            using var previous = Process.GetProcessById(processId);
            if (!previous.WaitForExit(WaitTimeout))
            {
                PluginLog.RestartWaitTimedOut(logger, processId, WaitTimeout);
            }
        }
        catch (ArgumentException)
        {
            // The previous process has already exited.
        }
    }
}
