using System.Security.Cryptography;

namespace Plugwright.NuGet;

/// <summary>
/// A temporary folder for the native libraries of plugin packages, one per process, deleted when the process exits.
/// </summary>
internal static class NativeLibraryFolder
{
    private static readonly Lazy<string> s_folder = new(CreateFolder);

    /// <summary>
    /// Writes a native library and checks right before returning that the file on disk is exactly the content.
    /// </summary>
    /// <returns>The path to load the library from.</returns>
    public static string Write(string pluginId, string fileName, byte[] content)
    {
        var folder = Directory.CreateDirectory(Path.Combine(s_folder.Value, pluginId)).FullName;
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, content);
        }

        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(content), SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new FileLoadException($"The native library '{path}' was changed after it was written and is not loaded.");
        }

        return path;
    }

    private static string CreateFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "plugwright-native", $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // Loaded libraries may still be locked (Windows); the folder is left behind.
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
        return folder;
    }
}
