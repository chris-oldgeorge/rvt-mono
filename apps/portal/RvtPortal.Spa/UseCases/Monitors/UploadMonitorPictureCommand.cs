// File summary: Handles CQRS commands for monitor deployment picture uploads.
// Major updates:
// - 2026-07-08 pending Replaced ASP.NET form-file dependency with the transport-neutral storage upload port.
// - 2026-07-08 pending Added picture-storage compensation when deployment persistence fails after save.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-06-09 pending Added MediatR command handler for monitor picture upload validation and persistence.

using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Application.Ports.Storage;
using RvtPortal.Spa.Api;

namespace RvtPortal.Spa.UseCases.Monitors;

public sealed record UploadMonitorPictureCommand(Guid MonitorId, IUploadedContent? Picture) : IRequest<UploadMonitorPictureResult>;

public sealed class UploadMonitorPictureResult
{
    public MonitorDetailResponse? Detail { get; set; }
    public bool NotFound { get; set; }
    public Dictionary<string, string[]> Errors { get; } = [];
}

public sealed class UploadMonitorPictureCommandHandler : IRequestHandler<UploadMonitorPictureCommand, UploadMonitorPictureResult>
{
    private const int MaxPictureBytes = 5 * 1024 * 1024;

    private readonly RVTDbContext _domainContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMonitorPictureStorage _pictureStorage;
    private readonly IMonitorDetailReader _detailReader;

    // Function summary: Initializes dependencies used to validate and persist monitor picture uploads.
    public UploadMonitorPictureCommandHandler(
        RVTDbContext domainContext,
        IHttpContextAccessor httpContextAccessor,
        IMonitorPictureStorage pictureStorage,
        IMonitorDetailReader detailReader)
    {
        _domainContext = domainContext;
        _httpContextAccessor = httpContextAccessor;
        _pictureStorage = pictureStorage;
        _detailReader = detailReader;
    }

    // Function summary: Handles monitor picture upload validation, storage, and detail response rebuilding.
    public async Task<UploadMonitorPictureResult> Handle(UploadMonitorPictureCommand request, CancellationToken cancellationToken)
    {
        UploadMonitorPictureResult result = new();
        Deployment? deployment = await _domainContext.Deployments
            .Include(item => item.Contract)
            .ThenInclude(contract => contract.Company)
            .Include(item => item.Contract)
            .ThenInclude(contract => contract.Site)
            .Include(item => item.Monitor)
            .Where(item => item.MonitorId == request.MonitorId && item.EndDate == null && !item.Monitor.Archived)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (deployment == null)
        {
            bool monitorExists = await _domainContext.MonitorsList.AnyAsync(item => item.Id == request.MonitorId && !item.Archived, cancellationToken);
            if (monitorExists)
            {
                AddError(result, "picture", "A current deployment is required before uploading a monitor picture.");
            }
            else
            {
                result.NotFound = true;
            }

            return result;
        }

        ValidatePicture(request.Picture, result);
        if (result.Errors.Count > 0 || request.Picture == null)
        {
            return result;
        }

        string storedPictureLink = await _pictureStorage.SaveAsync(deployment.Id, request.Picture, cancellationToken);
        try
        {
            deployment.PictureLink = storedPictureLink;
            await _domainContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _pictureStorage.DeleteAsync(storedPictureLink, CancellationToken.None);
            throw;
        }

        ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
        if (user == null)
        {
            result.NotFound = true;
            return result;
        }

        result.Detail = await _detailReader.BuildAsync(deployment.Monitor, deployment, user, cancellationToken);
        return result;
    }

    // Function summary: Validates monitor picture upload shape before storage.
    private static void ValidatePicture(IUploadedContent? picture, UploadMonitorPictureResult result)
    {
        if (picture == null || picture.Length == 0)
        {
            AddError(result, "picture", "Select an image to upload.");
            return;
        }

        if (picture.Length > MaxPictureBytes)
        {
            AddError(result, "picture", "Monitor pictures must be 5 MB or smaller.");
            return;
        }

        string extension = Path.GetExtension(picture.FileName).ToLowerInvariant();
        bool supportedExtension = extension is ".jpg" or ".jpeg" or ".png" or ".webp";
        if (!supportedExtension || !picture.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || !HasSupportedImageHeader(picture))
        {
            AddError(result, "picture", "Upload a JPG, PNG, or WebP image.");
        }
    }

    // Function summary: Checks image magic bytes before accepting a monitor picture.
    private static bool HasSupportedImageHeader(IUploadedContent picture)
    {
        Span<byte> header = stackalloc byte[12];
        using Stream stream = picture.OpenReadStream();
        int read = stream.Read(header);
        bool isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        bool isPng = read >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        bool isWebp = read >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
        return isJpeg || isPng || isWebp;
    }

    // Function summary: Appends a validation error to a command result.
    private static void AddError(UploadMonitorPictureResult result, string key, string message)
    {
        result.Errors[key] = result.Errors.TryGetValue(key, out string[]? existing)
            ? [.. existing, message]
            : [message];
    }
}
