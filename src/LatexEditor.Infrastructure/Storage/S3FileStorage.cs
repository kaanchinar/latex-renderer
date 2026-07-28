using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.Storage;

/// <summary>
/// <see cref="IFileStorage"/> implementation backed by an S3-compatible object store
/// (MinIO for local development, Cloudflare R2 in production). Path-style addressing is
/// forced for compatibility with non-AWS endpoints.
/// </summary>
public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    /// <inheritdoc />
    public StorageProvider Provider => StorageProvider.S3;

    /// <summary>
    /// Creates the storage client from the configured <see cref="StorageOptions"/>.
    /// The target bucket must already exist; it is not created automatically.
    /// </summary>
    public S3FileStorage(IOptions<StorageOptions> options)
    {
        var opts = options.Value;
        _bucket = opts.S3Bucket;

        var config = new AmazonS3Config
        {
            ServiceURL = opts.S3ServiceUrl,
            ForcePathStyle = true
        };
        _client = new AmazonS3Client(
            new BasicAWSCredentials(opts.S3AccessKey, opts.S3SecretKey), config);
    }

    /// <inheritdoc />
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType
        }, ct);
    }

    /// <inheritdoc />
    public async Task<Stream?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, key, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(_bucket, key, ct);
    }

    /// <inheritdoc />
    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET
        });
        return Task.FromResult(url);
    }
}
