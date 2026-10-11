using System.Diagnostics;
using System.Reflection;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Restarts in a process of their own: tests/RestartHost requests a restart as soon as it has started.
/// </summary>
public sealed class RestartTests : IDisposable
{
    private static readonly string s_restartHostPath = typeof(RestartTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "RestartHostPath")
        .Value!;

    private readonly TempDirectory _directory = new();

    private string LogFile => Path.Combine(_directory.Path, "restart.log");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task ExitCode_mode_stops_the_application_with_exit_code_75()
    {
        var exitCode = await RunAsync(useAppHost: false, "exitcode");

        exitCode.ShouldBe(75);
        File.ReadAllLines(LogFile).ShouldBe(["first:start", "first:applied", "first:end"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Relaunch_mode_starts_a_new_process_that_waits_for_the_old_one(bool useAppHost)
    {
        if (useAppHost && !File.Exists(AppHostPath))
        {
            Assert.Skip("The application host of RestartHost was not built.");
        }

        var exitCode = await RunAsync(useAppHost, "relaunch");

        exitCode.ShouldBe(0);
        var lines = await WaitForLineAsync("second:end");
        lines.ShouldContain("first:end");
        // The new process applies its plugins only after the old one has exited.
        lines.IndexOf("first:end").ShouldBeLessThan(lines.IndexOf("second:applied"));
        lines.Count(line => line == "second:start").ShouldBe(1);
    }

    private static string AppHostPath => Path.Combine(s_restartHostPath, OperatingSystem.IsWindows() ? "RestartHost.exe" : "RestartHost");

    private async Task<int> RunAsync(bool useAppHost, string mode)
    {
        var startInfo = useAppHost ? new ProcessStartInfo(AppHostPath) : new ProcessStartInfo("dotnet");
        if (!useAppHost)
        {
            startInfo.ArgumentList.Add(Path.Combine(s_restartHostPath, "RestartHost.dll"));
        }

        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(LogFile);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        TestContext.Current.TestOutputHelper?.WriteLine(await output + await error);
        return process.ExitCode;
    }

    private async Task<List<string>> WaitForLineAsync(string line)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            var lines = File.ReadAllLines(LogFile).ToList();
            if (lines.Contains(line) || DateTime.UtcNow > deadline)
            {
                return lines;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
