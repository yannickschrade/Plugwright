namespace Plugwright.IntegrationTests;

/// <summary>
/// A temporary directory for one test, deleted when the test ends.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = Directory.CreateTempSubdirectory("plugwright-").FullName;
    }

    public string Path { get; }

    public void Dispose()
    {
        // Assemblies loaded from here stay loaded (load contexts are not collectible).
        // On Windows their files are locked, so cleanup is best effort.
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
