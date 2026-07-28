using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Rvt.Storage.S3;

public sealed class S3ObjectStorageClient : IObjectStorageClient, IDisposable
{
    private readonly IAmazonS3 client;
    private readonly string bucket;
    private readonly string prefix;
    private readonly string resourceName;

    public S3ObjectStorageClient(
        string resourceName,
        S3StorageOptions options)
    {
        ValidateResourceName(resourceName);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Bucket))
        {
            throw new InvalidOperationException(
                "S3 object storage requires a non-empty RVT__S3_BUCKET.");
        }

        this.resourceName = resourceName;
        bucket = options.Bucket.Trim();
        prefix = NormalizePrefix(options.Prefix);
        client = new AmazonS3Client(CreateClientConfiguration(options));
    }

    internal S3ObjectStorageClient(
        string resourceName,
        IAmazonS3 client,
        string bucket,
        string prefix)
    {
        ValidateResourceName(resourceName);
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new ArgumentException("S3 bucket cannot be blank.", nameof(bucket));
        }

        this.resourceName = resourceName;
        this.bucket = bucket.Trim();
        this.prefix = NormalizePrefix(prefix);
    }

    public async Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Key);
        ArgumentNullException.ThrowIfNull(request.Content);

        var putRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = GetProviderKey(request.Key),
            InputStream = request.Content,
            AutoCloseStream = false,
        };
        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            putRequest.ContentType = request.ContentType;
        }

        try
        {
            await client.PutObjectAsync(putRequest, cancellationToken);
            return new StorageWriteResult(request.Key);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw TranslateCancellation(exception, request.Key);
        }
        catch (AmazonS3Exception exception)
        {
            throw TranslateFailure(exception, request.Key);
        }
        catch (AmazonServiceException exception)
        {
            throw TranslateServiceFailure(exception, request.Key);
        }
        catch (AmazonClientException exception)
        {
            throw TranslateClientFailure(exception, request.Key);
        }
    }

    public async Task<StorageReadResult?> OpenReadAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            GetObjectResponse response = await client.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = bucket,
                    Key = GetProviderKey(key),
                },
                cancellationToken);
            return new StorageReadResult(
                response.ResponseStream,
                response.Headers.ContentType,
                response.Headers.ContentLength,
                response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw TranslateCancellation(exception, key);
        }
        catch (AmazonS3Exception exception) when (IsMissing(exception))
        {
            return null;
        }
        catch (AmazonS3Exception exception)
        {
            throw TranslateFailure(exception, key);
        }
        catch (AmazonServiceException exception)
        {
            throw TranslateServiceFailure(exception, key);
        }
        catch (AmazonClientException exception)
        {
            throw TranslateClientFailure(exception, key);
        }
    }

    public async Task<bool> DeleteIfExistsAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        string providerKey = GetProviderKey(key);
        try
        {
            await client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest
                {
                    BucketName = bucket,
                    Key = providerKey,
                },
                cancellationToken);
            await client.DeleteObjectAsync(
                new DeleteObjectRequest
                {
                    BucketName = bucket,
                    Key = providerKey,
                },
                cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw TranslateCancellation(exception, key);
        }
        catch (AmazonS3Exception exception) when (IsMissing(exception))
        {
            return false;
        }
        catch (AmazonS3Exception exception)
        {
            throw TranslateFailure(exception, key);
        }
        catch (AmazonServiceException exception)
        {
            throw TranslateServiceFailure(exception, key);
        }
        catch (AmazonClientException exception)
        {
            throw TranslateClientFailure(exception, key);
        }
    }

    public Uri GetObjectUri(StorageObjectKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        string escapedProviderKey = string.Join(
            '/',
            GetProviderKey(key).Split('/').Select(Uri.EscapeDataString));
        return new Uri($"s3://{bucket}/{escapedProviderKey}");
    }

    public void Dispose() => client.Dispose();

    internal static AmazonS3Config CreateClientConfiguration(
        S3StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var config = new AmazonS3Config { ForcePathStyle = options.ForcePathStyle };
        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            if (!Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out Uri? serviceUri))
            {
                throw new InvalidOperationException(
                    "RVT__S3_SERVICE_URL must be an absolute URI.");
            }

            config.ServiceURL = serviceUri.AbsoluteUri.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(options.Region))
            {
                config.AuthenticationRegion = options.Region.Trim();
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.Region))
        {
            config.RegionEndpoint =
                RegionEndpoint.GetBySystemName(options.Region.Trim());
        }

        return config;
    }

    private static string NormalizePrefix(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : StorageObjectKey.Parse(value).Value;

    private string GetProviderKey(StorageObjectKey key) =>
        string.IsNullOrEmpty(prefix)
            ? key.Value
            : $"{prefix}/{key.Value}";

    private ObjectStorageException TranslateFailure(
        AmazonS3Exception exception,
        StorageObjectKey key) =>
        new(
            ClassifyFailure(exception.StatusCode),
            resourceName,
            key,
            exception);

    /// <summary>
    /// Covers the SDK failures that never reached a service response —
    /// missing credentials, DNS and endpoint faults — which are transport
    /// problems rather than a classified service rejection.
    /// </summary>
    private ObjectStorageException TranslateClientFailure(
        AmazonClientException exception,
        StorageObjectKey key) =>
        new(
            StorageFailureKind.Unavailable,
            resourceName,
            key,
            exception);

    /// <summary>
    /// Non-S3 service rejections (for example STS credential resolution)
    /// still carry a status code, so they classify like any other response.
    /// </summary>
    private ObjectStorageException TranslateServiceFailure(
        AmazonServiceException exception,
        StorageObjectKey key) =>
        new(
            ClassifyFailure(exception.StatusCode),
            resourceName,
            key,
            exception);

    private ObjectStorageException TranslateCancellation(
        OperationCanceledException exception,
        StorageObjectKey key) =>
        new(
            StorageFailureKind.Unavailable,
            resourceName,
            key,
            exception);

    private static bool IsMissing(AmazonS3Exception exception) =>
        exception.StatusCode == System.Net.HttpStatusCode.NotFound
        || string.Equals(
            exception.ErrorCode,
            "NoSuchKey",
            StringComparison.Ordinal);

    private static StorageFailureKind ClassifyFailure(
        System.Net.HttpStatusCode statusCode)
    {
        int status = (int)statusCode;
        return status switch
        {
            403 => StorageFailureKind.AccessDenied,
            409 => StorageFailureKind.Conflict,
            408 or 429 => StorageFailureKind.Unavailable,
            >= 500 => StorageFailureKind.Unavailable,
            >= 400 and < 500 => StorageFailureKind.InvalidRequest,
            _ => StorageFailureKind.Unknown,
        };
    }

    private static void ValidateResourceName(string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            throw new ArgumentException(
                "Object storage resource name cannot be blank.",
                nameof(resourceName));
        }
    }
}
