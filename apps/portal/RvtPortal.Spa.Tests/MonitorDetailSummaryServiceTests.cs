using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RvtPortal.Spa.Api;
using RvtPortal.Spa.Application.Monitors;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Tests;

public sealed class MonitorDetailSummaryServiceTests
{
    [Fact]
    public async Task BuildLatestAverageAsync_WhenDataSourceCancels_PropagatesCancellation()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        using RVTSearchContext searchContext = new(
            new DbContextOptionsBuilder<RVTSearchContext>()
                .UseInMemoryDatabase($"monitor-summary-{Guid.NewGuid():N}")
                .Options);
        MonitorDetailSummaryService subject = new(
            searchContext,
            new ThrowingMonitorDataSource(
                new OperationCanceledException(cancellation.Token)),
            new RecordingLogger<MonitorDetailSummaryService>());
        Deployment deployment = new()
        {
            Id = Guid.NewGuid(),
            Monitor = new MonitorEntity
            {
                Id = Guid.NewGuid(),
                TypeOfMonitor = MonitorTypeEnum.Noise
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => subject.BuildLatestAverageAsync(deployment));
    }

    [Fact]
    public async Task BuildLatestReadingAsync_WhenDataSourceFails_LogsAndUsesNotificationFallback()
    {
        IOException failure = new("measurement store unavailable");
        RecordingLogger<MonitorDetailSummaryService> logger = new();
        using RVTSearchContext searchContext = new(
            new DbContextOptionsBuilder<RVTSearchContext>()
                .UseInMemoryDatabase($"monitor-summary-{Guid.NewGuid():N}")
                .Options);
        MonitorDetailSummaryService subject = new(
            searchContext,
            new ThrowingMonitorDataSource(failure),
            logger);
        Guid deploymentId = Guid.NewGuid();
        Deployment deployment = new()
        {
            Id = deploymentId,
            Monitor = new MonitorEntity
            {
                Id = Guid.NewGuid(),
                TypeOfMonitor = MonitorTypeEnum.Noise
            }
        };
        Notification fallback = new()
        {
            AlertField = "LAeq",
            AlertType = AlertTypeEnum.Alert,
            Level = 72.5,
            NotificationTime = new DateTime(2026, 7, 29, 9, 30, 0, DateTimeKind.Utc)
        };

        MonitorMetricSummary? result = await subject.BuildLatestReadingAsync(
            deployment,
            fallback);

        RecordedLogEntry entry = Assert.Single(logger.Entries);
        Assert.NotNull(result);
        Assert.Equal("LAeq", result.Field);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.Contains(deploymentId.ToString(), entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildLatestAverageAsync_WhenCallerAlreadyCancelled_DoesNotStartDataRead()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        CountingMonitorDataSource dataSource = new();
        using RVTSearchContext searchContext = new(
            new DbContextOptionsBuilder<RVTSearchContext>()
                .UseInMemoryDatabase($"monitor-summary-{Guid.NewGuid():N}")
                .Options);
        MonitorDetailSummaryService subject = new(
            searchContext,
            dataSource,
            new RecordingLogger<MonitorDetailSummaryService>());
        Deployment deployment = new()
        {
            Id = Guid.NewGuid(),
            Monitor = new MonitorEntity
            {
                Id = Guid.NewGuid(),
                TypeOfMonitor = MonitorTypeEnum.Noise
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => subject.BuildLatestAverageAsync(
                deployment,
                cancellation.Token));

        Assert.Equal(0, dataSource.CallCount);
    }

    private sealed class ThrowingMonitorDataSource(Exception exception)
        : IMonitorDataSource
    {
        public Task<MonitorData> GetDeploymentDataAsync(
            DeploymentDataQuery request,
            CancellationToken cancellationToken = default) =>
            Task.FromException<MonitorData>(exception);

        public Task<IReadOnlyList<OmnidotsTracesIndex>> GetTraceIndexesAsync(
            string serialId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OmnidotsTracesIndex?> GetTraceIndexAsync(Guid traceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CountingMonitorDataSource : IMonitorDataSource
    {
        public int CallCount { get; private set; }

        public Task<MonitorData> GetDeploymentDataAsync(
            DeploymentDataQuery request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new MonitorData());
        }

        public Task<IReadOnlyList<OmnidotsTracesIndex>> GetTraceIndexesAsync(
            string serialId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OmnidotsTracesIndex?> GetTraceIndexAsync(Guid traceId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

internal sealed record RecordedLogEntry(
    LogLevel Level,
    string Message,
    Exception? Exception);

internal sealed class RecordingLogger<TCategory> : ILogger<TCategory>
{
    private readonly ConcurrentQueue<RecordedLogEntry> _entries = new();

    public IReadOnlyCollection<RecordedLogEntry> Entries => [.. _entries];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Enqueue(new RecordedLogEntry(
            logLevel,
            formatter(state, exception),
            exception));
    }
}
