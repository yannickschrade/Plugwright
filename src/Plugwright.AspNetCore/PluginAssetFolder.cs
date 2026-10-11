namespace Plugwright.AspNetCore;

/// <summary>
/// A temporary folder for the static web assets of plugins, one per process, deleted when the process exits.
/// </summary>
internal static class PluginAssetFolder
{
    private static readonly Lazy<string> s_folder = new(CreateFolder);

    public static string Path => s_folder.Value;

    /// <summary>
    /// Writes files below a subfolder and returns the subfolder.
    /// </summary>
    public static string Write(string name, IEnumerable<KeyValuePair<string, byte[]>> files)
    {
        var folder = Directory.CreateDirectory(System.IO.Path.Combine(Path, name)).FullName;
        foreach ((var relativePath, var content) in files)
        {
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, relativePath));
            if (!path.StartsWith(folder + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The static web asset '{relativePath}' of '{name}' lies outside its folder.");
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }

        return folder;
    }

    private static string CreateFolder()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plugwright-assets", $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // Files may still be open (Windows); the folder is left behind.
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
        return folder;
    }
}
