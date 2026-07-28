using LatexEditor.Core.Entities;

namespace LatexEditor.Core.Interfaces;

/// <summary>
/// Key-value object storage for file contents (source files, binary assets, generated PDFs).
/// Keys are opaque strings; metadata and listing live in PostgreSQL, so no enumeration API is provided.
/// </summary>
public interface IFileStorage
{
    /// <summary>The storage backend this instance writes to, recorded on file metadata.</summary>
    StorageProvider Provider { get; }

    /// <summary>
    /// Writes content under the given key, creating or overwriting it.
    /// </summary>
    /// <param name="key">Storage key (e.g. <c>{projectId}/{fileId}</c>).</param>
    /// <param name="content">Content stream; read from its current position to the end.</param>
    /// <param name="contentType">MIME type, used by S3 backends when serving the object.</param>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Opens the content stored under the given key for reading.
    /// </summary>
    /// <returns>A readable stream, or <c>null</c> if the key does not exist. The caller disposes the stream.</returns>
    Task<Stream?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Returns whether the given key exists, without downloading its content.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>Deletes the content under the given key. No-op if it does not exist.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns a short-lived URL from which the object can be downloaded directly
    /// (presigned URL on S3 backends; a server route on local disk).
    /// </summary>
    /// <param name="key">Storage key of the object.</param>
    /// <param name="expiry">How long the URL remains valid.</param>
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}
