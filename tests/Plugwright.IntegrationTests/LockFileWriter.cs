using System.Security.Cryptography;
using System.Text.Json;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Writes lock files for the HashVerifier the way a deployment would: the hash of every file in every plugin folder.
/// Written independently of Plugwright's code, so that the tests also check the file format.
/// </summary>
internal static class LockFileWriter
{
    public static void Write(string lockFilePath, string pluginDirectory)
    {
        var plugins = Directory.EnumerateDirectories(pluginDirectory).Order(StringComparer.Ordinal).ToDictionary(
            folder => Path.GetFileName(folder),
            folder => new
            {
                files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToDictionary(
                    file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'),
                    file => "sha256-" + Convert.ToBase64String(SHA256.HashData(File.ReadAllBytes(file)))),
            });

        File.WriteAllText(lockFilePath, JsonSerializer.Serialize(new { version = 1, plugins }));
    }
}
