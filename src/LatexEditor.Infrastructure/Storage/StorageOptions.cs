namespace LatexEditor.Infrastructure.Storage;

/// <summary>
/// Configuration for file storage, bound from the <c>Storage</c> configuration section
/// (or <c>Storage__*</c> environment variables).
/// </summary>
public class StorageOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Storage";

    /// <summary>Storage backend to use: <c>Local</c> (default) or <c>S3</c>.</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Root directory for local disk storage. Relative paths resolve against the working directory.</summary>
    public string LocalRootPath { get; set; } = "storage";

    /// <summary>S3 bucket name.</summary>
    public string S3Bucket { get; set; } = string.Empty;

    /// <summary>S3 service URL (e.g. <c>http://minio:9000</c> or the R2 endpoint).</summary>
    public string S3ServiceUrl { get; set; } = string.Empty;

    /// <summary>S3 access key. Load from environment variables, never commit to source control.</summary>
    public string S3AccessKey { get; set; } = string.Empty;

    /// <summary>S3 secret key. Load from environment variables, never commit to source control.</summary>
    public string S3SecretKey { get; set; } = string.Empty;
}
