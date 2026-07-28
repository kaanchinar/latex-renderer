using LatexEditor.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.Tests;

public class S3FileStorageTests
{
    private static S3FileStorage CreateStorage(Action<StorageOptions> configure)
    {
        var options = new StorageOptions
        {
            S3Bucket = "test-bucket",
            S3ServiceUrl = "https://example.r2.cloudflarestorage.com",
            S3AccessKey = "key",
            S3SecretKey = "secret"
        };
        configure(options);
        return new S3FileStorage(Options.Create(options));
    }

    [Fact]
    public async Task PresignedUrl_UsesConfiguredRegionInCredentialScope()
    {
        var storage = CreateStorage(o => o.S3Region = "auto");

        var url = await storage.GetPresignedUrlAsync("some/key.pdf", TimeSpan.FromMinutes(15));

        Assert.Contains("X-Amz-Credential=key%2F", url);
        Assert.Contains("%2Fauto%2F", url);
    }

    [Fact]
    public async Task PresignedUrl_DefaultsToUsEast1WithoutRegion()
    {
        var storage = CreateStorage(_ => { });

        var url = await storage.GetPresignedUrlAsync("some/key.pdf", TimeSpan.FromMinutes(15));

        Assert.Contains("%2Fus-east-1%2F", url);
    }

    [Fact]
    public async Task PresignedUrl_UsesPublicUrlWhenConfigured()
    {
        var storage = CreateStorage(o =>
        {
            o.S3ServiceUrl = "http://minio:9000";
            o.S3PublicUrl = "http://localhost:9000";
        });

        var url = await storage.GetPresignedUrlAsync("some/key.pdf", TimeSpan.FromMinutes(15));

        Assert.StartsWith("http://localhost:9000/test-bucket/some/key.pdf", url);
    }

    [Fact]
    public async Task PresignedUrl_FallsBackToServiceUrlWithoutPublicUrl()
    {
        var storage = CreateStorage(o => o.S3ServiceUrl = "https://internal.example.com");

        var url = await storage.GetPresignedUrlAsync("some/key.pdf", TimeSpan.FromMinutes(15));

        Assert.StartsWith("https://internal.example.com/test-bucket/some/key.pdf", url);
    }

    [Fact]
    public void Provider_IsS3()
    {
        Assert.Equal(Core.Entities.StorageProvider.S3, CreateStorage(_ => { }).Provider);
    }
}
