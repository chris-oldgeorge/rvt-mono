using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace Rvt.Monitor.Common.Storage;

public sealed class S3BlobStorageService : IBlobStorageService, IDisposable
{
    private readonly IAmazonS3 client;
    private readonly string bucket;
    private readonly string prefix;

    public S3BlobStorageService(BlobStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.S3Bucket))
        {
            throw new InvalidOperationException("S3 blob storage requires a non-empty RVT__S3_BUCKET.");
        }

        bucket = options.S3Bucket.Trim();
        prefix = NormalizePrefix(options.Prefix);
        client = new AmazonS3Client(CreateClientConfiguration(options));
    }

    public async Task<BlobStorageWriteResult> WriteAsync(
        BlobStorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var objectName = BlobObjectName.Normalize(request.ObjectName);
        var providerObjectName = GetProviderObjectName(objectName);
        await using var stream = new MemoryStream(request.Content, writable: false);
        var putRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = providerObjectName,
            InputStream = stream,
            AutoCloseStream = false
        };

        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            putRequest.ContentType = request.ContentType;
        }

        await client.PutObjectAsync(putRequest, cancellationToken);
        return new BlobStorageWriteResult(objectName, BuildProviderUri(providerObjectName));
    }

    public async Task DeleteAsync(string objectName, CancellationToken cancellationToken = default)
    {
        var normalizedObjectName = BlobObjectName.Normalize(objectName);
        await client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = bucket,
            Key = GetProviderObjectName(normalizedObjectName)
        }, cancellationToken);
    }

    public void Dispose()
    {
        client.Dispose();
    }

    private static AmazonS3Config CreateClientConfiguration(BlobStorageOptions options)
    {
        var configuration = new AmazonS3Config
        {
            ForcePathStyle = options.S3ForcePathStyle
        };

        if (!string.IsNullOrWhiteSpace(options.S3ServiceUrl))
        {
            if (!Uri.TryCreate(options.S3ServiceUrl, UriKind.Absolute, out var serviceUri))
            {
                throw new InvalidOperationException("RVT__S3_SERVICE_URL must be an absolute URI.");
            }

            configuration.ServiceURL = serviceUri.AbsoluteUri.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(options.S3Region))
            {
                configuration.AuthenticationRegion = options.S3Region.Trim();
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.S3Region))
        {
            configuration.RegionEndpoint = RegionEndpoint.GetBySystemName(options.S3Region.Trim());
        }

        return configuration;
    }

    private string GetProviderObjectName(string objectName)
    {
        return string.IsNullOrEmpty(prefix) ? objectName : $"{prefix}/{objectName}";
    }

    private string BuildProviderUri(string providerObjectName)
    {
        var escapedObjectName = string.Join('/', providerObjectName.Split('/').Select(Uri.EscapeDataString));
        return $"s3://{bucket}/{escapedObjectName}";
    }

    private static string NormalizePrefix(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : BlobObjectName.Normalize(value);
    }
}
