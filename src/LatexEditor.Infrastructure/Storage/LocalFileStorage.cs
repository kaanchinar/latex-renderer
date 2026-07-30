using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.Storage;

/// <summary>
/// <see cref="IFileStorage"/> implementation that stores objects as files under a local
/// root directory. Development default. Keys map to paths relative to the root;
/// keys escaping the root (path traversal) are rejected.
/// </summary>
public class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _rootPath = Path.GetFullPath(options.Value.LocalRootPath);

    /// <inheritdoc />
    public StorageProvider Provider => StorageProvider.Local;

    private string ResolvePath(string key)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, key));
        return !fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar) ? throw new ArgumentException($"Invalid storage key: {key}", nameof(key)) : fullPath;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The key resolves outside the storage root.</exception>
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, ct);
    }

    /// <inheritdoc />
    public Task<Stream?> GetAsync(string key, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        if (!File.Exists(fullPath)) return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(ResolvePath(key)));
    }

    /// <inheritdoc />
    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(key);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns a server-relative <c>/files/{key}</c> route served by the API rather than
    /// a cryptographically signed URL. Keys are server-generated (GUIDs and slashes
    /// only), so no escaping is applied — escaped slashes would not route correctly.
    /// </remarks>
    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        return Task.FromResult($"/files/{key}");
    }
}
