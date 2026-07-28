using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Rvt.Storage.AzureBlob;

public sealed class AzureBlobObjectStorageClient : IObjectStorageClient
{
    private readonly BlobContainerClient containerClient;
    private readonly string prefix;
    private readonly string resourceName;

    public AzureBlobObjectStorageClient(
        string resourceName,
        AzureBlobStorageOptions options)
    {
        ValidateResourceName(resourceName);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Container))
        {
            throw new InvalidOperationException(
                "Azure Blob storage requires a non-empty blob container.");
        }

        this.resourceName = resourceName;
        prefix = NormalizePrefix(options.Prefix);
        containerClient = CreateServiceClient(options)
            .GetBlobContainerClient(options.Container.Trim());
    }

    internal AzureBlobObjectStorageClient(
        string resourceName,
        BlobContainerClient containerClient,
        string prefix)
    {
        ValidateResourceName(resourceName);
        this.containerClient =
            containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        this.resourceName = resourceName;
        this.prefix = NormalizePrefix(prefix);
    }

    public async Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Key);
        ArgumentNullException.ThrowIfNull(request.Content);

        BlobClient blobClient = GetBlobClient(request.Key);
        try
        {
            await containerClient.CreateIfNotExistsAsync(
                cancellationToken: cancellationToken);
            await blobClient.UploadAsync(
                request.Content,
                new BlobUploadOptions
                {
                    HttpHeaders = string.IsNullOrWhiteSpace(request.ContentType)
                        ? null
                        : new BlobHttpHeaders { ContentType = request.ContentType },
                },
                cancellationToken);
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
        catch (RequestFailedException exception)
        {
            throw TranslateFailure(exception, request.Key);
        }
        catch (AuthenticationFailedException exception)
        {
            throw TranslateAuthenticationFailure(exception, request.Key);
        }
    }

    public async Task<StorageReadResult?> OpenReadAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            Response<BlobDownloadStreamingResult> response = await GetBlobClient(key).DownloadStreamingAsync(
                new BlobDownloadOptions(),
                cancellationToken);
            return new StorageReadResult(
                response.Value.Content,
                response.Value.Details.ContentType,
                response.Value.Details.ContentLength,
                response.GetRawResponse());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw TranslateCancellation(exception, key);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (RequestFailedException exception)
        {
            throw TranslateFailure(exception, key);
        }
        catch (AuthenticationFailedException exception)
        {
            throw TranslateAuthenticationFailure(exception, key);
        }
    }

    public async Task<bool> DeleteIfExistsAsync(
        StorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            Response<bool> response = await GetBlobClient(key).DeleteIfExistsAsync(
                cancellationToken: cancellationToken);
            return response.Value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw TranslateCancellation(exception, key);
        }
        catch (RequestFailedException exception)
        {
            throw TranslateFailure(exception, key);
        }
        catch (AuthenticationFailedException exception)
        {
            throw TranslateAuthenticationFailure(exception, key);
        }
    }

    public Uri GetObjectUri(StorageObjectKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return GetBlobClient(key).Uri;
    }

    private static BlobServiceClient CreateServiceClient(AzureBlobStorageOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new BlobServiceClient(options.ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(options.ServiceUri))
        {
            if (!Uri.TryCreate(options.ServiceUri, UriKind.Absolute, out Uri? serviceUri))
            {
                throw new InvalidOperationException(
                    "RVT__BLOB_SERVICE_URI must be an absolute URI.");
            }

            return new BlobServiceClient(serviceUri, new DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            "Azure Blob storage requires RVT__BLOB_CONNECTION_STRING or RVT__BLOB_SERVICE_URI.");
    }

    private static string NormalizePrefix(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : StorageObjectKey.Parse(value).Value;

    private BlobClient GetBlobClient(StorageObjectKey key)
    {
        var providerKey = string.IsNullOrEmpty(prefix)
            ? key.Value
            : $"{prefix}/{key.Value}";
        return containerClient.GetBlobClient(providerKey);
    }

    private ObjectStorageException TranslateFailure(
        RequestFailedException exception,
        StorageObjectKey key) =>
        new(
            ClassifyFailure(exception.Status),
            resourceName,
            key,
            exception);

    /// <summary>
    /// Credential resolution failures arrive as
    /// <see cref="AuthenticationFailedException"/>, which is not a
    /// <see cref="RequestFailedException"/> and therefore previously crossed
    /// the port untranslated.
    /// </summary>
    private ObjectStorageException TranslateAuthenticationFailure(
        AuthenticationFailedException exception,
        StorageObjectKey key) =>
        new(
            StorageFailureKind.AccessDenied,
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

    private static StorageFailureKind ClassifyFailure(int status) =>
        status switch
        {
            403 => StorageFailureKind.AccessDenied,
            409 => StorageFailureKind.Conflict,
            408 or 429 => StorageFailureKind.Unavailable,
            >= 500 => StorageFailureKind.Unavailable,
            >= 400 and < 500 => StorageFailureKind.InvalidRequest,
            _ => StorageFailureKind.Unknown,
        };

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
