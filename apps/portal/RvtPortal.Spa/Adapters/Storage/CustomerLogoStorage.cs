// File summary: Stores customer report logos under application content and streams them through protected API routes.
// Major updates:
// - 2026-07-08 pending Moved customer-logo storage behind a business-layer storage port.
// - 2026-06-25 pending Kept logo content-type lookup as a concrete dictionary for CA1859 cleanup.
// - 2026-06-24 pending Added deterministic site customer-logo storage for report branding.
// - 2026-06-25 pending Added image magic-byte validation so non-image payloads cannot be stored as logos.
// - 2026-07-08 pending Made logo replacement write-through-temp so failed copies preserve the previous logo.

using RVT.BusinessLogic.Ports.Storage;

namespace RvtPortal.Spa.Adapters.Storage;

public sealed class CustomerLogoStorage : ICustomerLogoStorage
{
    private const long MaximumLogoBytes = 2 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp"
    };
    private static readonly string[] KnownExtensions = [".png", ".jpg", ".jpeg", ".webp"];
    private readonly IWebHostEnvironment environment;

    // Function summary: Initializes this type with the host paths needed for application-content storage.
    public CustomerLogoStorage(IWebHostEnvironment environment)
    {
        this.environment = environment;
    }

    public async Task SaveAsync(Guid siteId, IUploadedContent logo, CancellationToken cancellationToken)
    {
        if (logo.Length <= 0)
        {
            throw new StorageValidationException("Choose a logo image before uploading.");
        }

        if (logo.Length > MaximumLogoBytes)
        {
            throw new StorageValidationException("Customer logo images must be 2 MB or smaller.");
        }

        if (!AllowedContentTypes.TryGetValue(logo.ContentType, out var extension))
        {
            extension = ExtensionForFileName(logo.FileName);
        }

        if (!KnownExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new StorageValidationException("Customer logos must be PNG, JPEG, or WebP images.");
        }

        if (!HasSupportedImageHeader(logo))
        {
            throw new StorageValidationException("Customer logos must be valid PNG, JPEG, or WebP images.");
        }

        var directory = StorageDirectory();
        Directory.CreateDirectory(directory);
        var normalizedExtension = NormalizeExtension(extension);
        var targetPath = Path.Combine(directory, $"{siteId:N}{normalizedExtension}");
        var temporaryPath = Path.Combine(directory, $".{siteId:N}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await logo.CopyToAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
            DeleteExistingExcept(siteId, normalizedExtension);
        }
        catch
        {
            DeleteFileIfExists(temporaryPath);
            throw;
        }
    }

    public Task<StoredContentFile?> OpenReadAsync(Guid siteId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = FindExistingPath(siteId);
        if (path == null)
        {
            return Task.FromResult<StoredContentFile?>(null);
        }

        return Task.FromResult<StoredContentFile?>(new StoredContentFile(
            File.OpenRead(path),
            ContentTypeForLogo(path),
            Path.GetFileName(path)));
    }

    public Task DeleteAsync(Guid siteId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteExisting(siteId);
        return Task.CompletedTask;
    }

    public string? BuildProtectedLink(Guid siteId)
    {
        return FindExistingPath(siteId) == null ? null : $"/api/sites/{siteId}/customer-logo";
    }

    private string StorageDirectory()
    {
        return Path.Combine(ContentRoot(), "App_Data", "customer-logos");
    }

    private string ContentRoot()
    {
        return string.IsNullOrWhiteSpace(environment.ContentRootPath)
            ? AppContext.BaseDirectory
            : environment.ContentRootPath;
    }

    private string? FindExistingPath(Guid siteId)
    {
        var directory = StorageDirectory();
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return KnownExtensions
            .Select(extension => Path.Combine(directory, $"{siteId:N}{extension}"))
            .FirstOrDefault(File.Exists);
    }

    private void DeleteExisting(Guid siteId)
    {
        DeleteExistingExcept(siteId, keepExtension: null);
    }

    private void DeleteExistingExcept(Guid siteId, string? keepExtension)
    {
        foreach (var extension in KnownExtensions)
        {
            if (extension.Equals(keepExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = Path.Combine(StorageDirectory(), $"{siteId:N}{extension}");
            DeleteFileIfExists(path);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string ExtensionForFileName(string? fileName)
    {
        return string.IsNullOrWhiteSpace(fileName)
            ? ""
            : Path.GetExtension(fileName).ToLowerInvariant();
    }

    private static string NormalizeExtension(string extension)
    {
        return extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : extension.ToLowerInvariant();
    }

    private static string ContentTypeForLogo(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    // Function summary: Checks image magic bytes so a non-image payload cannot be stored as a logo.
    private static bool HasSupportedImageHeader(IUploadedContent logo)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = logo.OpenReadStream();
        var read = stream.Read(header);
        var isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        var isWebp = read >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
        return isJpeg || isPng || isWebp;
    }
}
