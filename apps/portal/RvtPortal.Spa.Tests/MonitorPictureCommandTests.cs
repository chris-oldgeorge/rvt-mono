// File summary: Verifies monitor picture command side-effect compensation around database persistence failures.
// Major updates:
// - 2026-07-08 pending Added coverage for cleaning up saved monitor pictures when deployment persistence fails.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RVT.BusinessLogic.Ports.Storage;
using RVT.DataAccess.Context;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Monitors;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Tests;

public sealed class MonitorPictureCommandTests
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    [Fact]
    // Function summary: Verifies a saved picture is deleted when the deployment row cannot be updated.
    public async Task UploadMonitorPictureCommand_DeletesSavedPictureWhenDatabaseSaveFails()
    {
        var monitorId = Guid.NewGuid();
        await using var context = await CreateCurrentDeploymentContextAsync(monitorId);
        var storage = new RecordingMonitorPictureStorage();
        var handler = new UploadMonitorPictureCommandHandler(
            context,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) } },
            storage,
            new StubMonitorDetailReader());
        context.ThrowOnSaveChanges = true;

        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(
            new UploadMonitorPictureCommand(
                monitorId,
                new MemoryUploadedContent("monitor.png", "image/png", PngBytes)),
            CancellationToken.None));

        Assert.Equal(storage.SavedLink, Assert.Single(storage.DeletedLinks));
    }

    // Function summary: Creates a domain context containing one active monitor deployment.
    private static async Task<ThrowingSaveRVTDbContext> CreateCurrentDeploymentContextAsync(Guid monitorId)
    {
        var options = new DbContextOptionsBuilder<RVTDbContext>()
            .UseInMemoryDatabase($"monitor-picture-command-{Guid.NewGuid():N}")
            .Options;
        var context = new ThrowingSaveRVTDbContext(options);
        var company = new Company { Id = Guid.NewGuid(), CompanyName = "Picture Company" };
        var site = new Site { Id = Guid.NewGuid(), SiteName = "Picture Site", CreateDate = DateTime.UtcNow };
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            ContractNumber = "PIC-1",
            CompanyId = company.Id,
            Company = company,
            SiteiD = site.Id,
            Site = site,
            OnHireDate = DateTime.UtcNow.AddDays(-1)
        };
        var monitor = new MonitorEntity
        {
            Id = monitorId,
            SerialId = "PIC-001",
            Manufacturer = "RVT",
            Model = "Picture",
            FirmwareVersion = "1",
            ListedAtTime = DateTime.UtcNow
        };
        var deployment = new Deployment
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            Contract = contract,
            MonitorId = monitor.Id,
            Monitor = monitor,
            StartDate = DateTime.UtcNow.AddHours(-1)
        };

        context.AddRange(company, site, contract, monitor, deployment);
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class ThrowingSaveRVTDbContext : RVTDbContext
    {
        public ThrowingSaveRVTDbContext(DbContextOptions<RVTDbContext> options)
            : base(options)
        {
        }

        public bool ThrowOnSaveChanges { get; set; }

        // Function summary: Simulates a database failure after the handler has saved external picture content.
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return ThrowOnSaveChanges
                ? throw new DbUpdateException("Simulated deployment persistence failure.")
                : base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class RecordingMonitorPictureStorage : IMonitorPictureStorage
    {
        public string SavedLink { get; } = "monitor-pictures/saved-monitor.png";
        public List<string> DeletedLinks { get; } = [];

        // Function summary: Records a successful picture save and returns a stored link.
        public Task<string> SaveAsync(Guid deploymentId, IUploadedContent picture, CancellationToken cancellationToken)
        {
            return Task.FromResult(SavedLink);
        }

        // Function summary: Records compensation deletes for saved monitor picture links.
        public Task DeleteAsync(string? storedLink, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(storedLink))
            {
                DeletedLinks.Add(storedLink);
            }

            return Task.CompletedTask;
        }

        // Function summary: No-op stream lookup for this command compensation test.
        public Task<StoredContentFile?> OpenReadAsync(string? storedLink, CancellationToken cancellationToken)
        {
            return Task.FromResult<StoredContentFile?>(null);
        }

        // Function summary: No-op link builder for this command compensation test.
        public string? BuildProtectedLink(Guid monitorId, string? storedLink)
        {
            return storedLink;
        }
    }

    private sealed class StubMonitorDetailReader : IMonitorDetailReader
    {
        // Function summary: The compensation test fails before detail rebuilding is reached.
        public Task<MonitorDetailResponse> BuildAsync(
            MonitorEntity monitor,
            Deployment? currentDeployment,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new MonitorDetailResponse { Id = monitor.Id });
        }
    }

    private sealed class MemoryUploadedContent : IUploadedContent
    {
        private readonly byte[] bytes;

        public MemoryUploadedContent(string fileName, string contentType, byte[] bytes)
        {
            FileName = fileName;
            ContentType = contentType;
            this.bytes = bytes;
        }

        public string FileName { get; }
        public string ContentType { get; }
        public long Length => bytes.Length;

        // Function summary: Opens the in-memory picture payload for command validation.
        public Stream OpenReadStream()
        {
            return new MemoryStream(bytes, writable: false);
        }

        // Function summary: Copies the in-memory picture payload to storage.
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken)
        {
            return target.WriteAsync(bytes, cancellationToken).AsTask();
        }
    }
}
