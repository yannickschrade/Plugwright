using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Plugwright.AspNetCore;

/// <summary>
/// Serves files from memory, for example the verified bytes of a plugin. The files have no physical path.
/// </summary>
internal sealed class MemoryFileProvider(IReadOnlyDictionary<string, byte[]> files) : IFileProvider
{
    private readonly DateTimeOffset _lastModified = DateTimeOffset.UtcNow;

    public IFileInfo GetFileInfo(string subpath) =>
        files.TryGetValue(subpath.TrimStart('/'), out var content)
            ? new MemoryFileInfo(Path.GetFileName(subpath), content, _lastModified)
            : new NotFoundFileInfo(subpath);

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private sealed class MemoryFileInfo(string name, byte[] content, DateTimeOffset lastModified) : IFileInfo
    {
        public bool Exists => true;

        public long Length => content.Length;

        public string? PhysicalPath => null;

        public string Name => name;

        public DateTimeOffset LastModified => lastModified;

        public bool IsDirectory => false;

        public Stream CreateReadStream() => new MemoryStream(content, writable: false);
    }
}
