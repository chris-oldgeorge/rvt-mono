// File summary: Pins the deployment-data default date window to the injected clock's UTC business day.
// Major updates:
// - 2026-07-30 pending Added when the DateTime.Today seeds were replaced per the TimeProvider/UTC ruling.

using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Querying;
using RvtPortal.Application.Time;
using RvtPortal.Spa.Application.Monitors;
using Monitor = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Tests;

public sealed class MonitorDataDefaultWindowTests
{
    [Fact]
    // Function summary: Verifies the default deployment-data window derives from the provider's UTC day, not the server-local date.
    public async Task GetDeploymentData_SeedsDefaultWindowFromProviderUtcDay()
    {
        // 23:30 UTC: every zone east of UTC is already on the next local day, so the old server-local
        // DateTime.Today seed would shift the whole default window by a day.
        DateTime utcNow = new(2026, 6, 10, 23, 30, 0, DateTimeKind.Utc);
        StubMonitorService monitorService = new(TestData.Deployment(Guid.NewGuid(), Guid.NewGuid()));

        MonitorData data = await MonitorData.GetDeploymentData(
            monitorService,
            new FixedUtcDateTimeProvider(utcNow),
            deploymentId: Guid.NewGuid(),
            traceId: null,
            filterOption: null,
            fromDate: null,
            toDate: null,
            graphData: false);

        Assert.Equal(new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc), data.FromDate);
        Assert.Equal(new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc), data.ToDate);
        Assert.Equal(DateTimeKind.Utc, data.FromDate.Kind);
        Assert.Equal(DateTimeKind.Utc, data.ToDate.Kind);
    }

    // Function summary: Supplies a deterministic UTC clock for the default-window seed.
    private sealed class FixedUtcDateTimeProvider(DateTime utcNow) : IRvtDateTimeProvider
    {
        public DateTime UtcNow => utcNow;

        public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public DateTime UtcToLocal(DateTime utcDateTime) => utcDateTime;

        public DateTime LocalToUtc(DateTime localDateTime) =>
            DateTime.SpecifyKind(localDateTime, DateTimeKind.Utc);

        public string DisplayUtcAsLocal(DateTime utcDateTime, string format) =>
            utcDateTime.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }

    // Function summary: Serves one deployment with no monitor so the default window is returned unclamped.
    private sealed class StubMonitorService(Deployment deployment) : IMonitorService
    {
        public Task<Monitor?> ReadOneAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Monitor?>(null);

        public Task<Deployment?> DeploymentReadOneAsync(Guid deploymentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Deployment?>(deployment);

        public Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels(string serialId, DateTime fromDate, DateTime toDate, int avrgDuration, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SearchQueryResult<MyAtmDustLevel>> GetMyAtmDustLevels8hourAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1hourAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevels1dayAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SearchQueryResult<NoiseLevel15minAvg>> GetAirQnoiseLevelsSiteAvg(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SearchQueryResult<OmnidotsPeakLevel>> GetOmnidotsPeakLevels(string serialId, DateTime fromDate, DateTime toDate, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortdir = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OmnidotsMonitorStatus?> GetVibrationMonitorStatusAsync(string serialId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SearchQueryResult<OmnidotsTrace>> GetVibrationTraces(Guid traceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OmnidotsTracesIndex?> TracesIndexReadOne(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
