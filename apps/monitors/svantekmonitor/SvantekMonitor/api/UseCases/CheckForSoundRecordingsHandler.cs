using Rvt.Monitor.Common.Storage;
using Svantek.Api.Db;
using Svantek.Api.Http;
using Svantek.Model.Http;

namespace Svantek.Api.UseCases;

// Summary: Checks Svantek alert notifications for matching audio recordings and uploads them to blob storage.
public sealed class CheckForSoundRecordingsHandler
{
    private readonly ISvantekNotificationQueries notificationQueries;
    private readonly ISvantekOperationalCommands operationalCommands;
    private readonly SvantekHttpGateway gateway;
    private readonly IBlobStorageService blobStorage;

    public CheckForSoundRecordingsHandler(
        ISvantekNotificationQueries notificationQueries,
        ISvantekOperationalCommands operationalCommands,
        SvantekHttpGateway gateway,
        IBlobStorageService blobStorage)
    {
        this.notificationQueries = notificationQueries;
        this.operationalCommands = operationalCommands;
        this.gateway = gateway;
        this.blobStorage = blobStorage;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var filesCache = new Dictionary<string, List<ProjectFile>>();
        var alerts = await notificationQueries
            .ReadLatestNotificationAsync(cancellationToken)
            .ConfigureAwait(false);
        var failures = new SvantekFailureCollector(operationalCommands);

        foreach (var alert in alerts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var dayCode = alert.NotificationTime.AddMinutes(-1).ToString("yyyyMMdd");
                var audioFiles = await FindFilesAsync(
                    filesCache,
                    dayCode,
                    alert.NotificationTime,
                    alert.AvgPeriod,
                    alert.ProjectId,
                    alert.PointId,
                    cancellationToken).ConfigureAwait(false);
                if (audioFiles.Count == 0)
                {
                    continue;
                }

                var audioFile = audioFiles.Count == 1
                    ? audioFiles[0]
                    : audioFiles.MinBy(file => Math.Abs((file.triggerDate - alert.NotificationTime).TotalSeconds))!;
                var content = await gateway.GetSoundFileAsync(
                    alert.ProjectId,
                    alert.PointId,
                    audioFile.stationType,
                    dayCode,
                    audioFile.stationSerial,
                    audioFile.filename,
                    cancellationToken).ConfigureAwait(false);
                var fileName = $"{alert.NotificationId}.wav";
                await blobStorage.WriteAsync(
                    new BlobStorageWriteRequest(fileName, content, "audio/wav"),
                    cancellationToken).ConfigureAwait(false);
                await operationalCommands.WriteSoundFileAsync(
                    alert.NotificationId,
                    fileName,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Capture(
                    $"sound:{alert.NotificationId}",
                    exception);
            }
        }

        failures.ThrowIfAny("CheckForSoundRecordings");
    }

    private async Task<List<ProjectFile>> FindFilesAsync(
        Dictionary<string, List<ProjectFile>> filesCache,
        string dayCode,
        DateTime alertTime,
        int averagePeriod,
        int projectId,
        int pointId,
        CancellationToken cancellationToken)
    {
        var files = await FetchFilesAsync(
            filesCache,
            dayCode,
            projectId,
            pointId,
            cancellationToken).ConfigureAwait(false);
        return files
            .Where(file => file.triggerDate >= alertTime.AddSeconds(-averagePeriod) &&
                           file.triggerDate <= alertTime)
            .ToList();
    }

    private async Task<List<ProjectFile>> FetchFilesAsync(
        Dictionary<string, List<ProjectFile>> filesCache,
        string dayCode,
        int projectId,
        int pointId,
        CancellationToken cancellationToken)
    {
        var listId = $"{projectId}:{pointId}:{dayCode}";
        if (filesCache.TryGetValue(listId, out var cached))
        {
            return cached;
        }

        var files = await gateway.GetProjectFilesAsync(
            projectId.ToString(),
            pointId.ToString(),
            dayCode,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var file in files)
        {
            ValidateFileRow(file);
        }

        var soundFiles = files
            .Where(file => file.filename.Contains(".WAV", StringComparison.Ordinal))
            .ToList();
        filesCache.Add(listId, soundFiles);
        return soundFiles;
    }

    private static void ValidateFileRow(ProjectFile file)
    {
        if (file.Count < 9)
        {
            throw new InvalidDataException(
                $"Svantek project file row contained {file.Count} fields; at least 9 are required.");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(file.filename) ||
                string.IsNullOrWhiteSpace(file.dayCode) ||
                string.IsNullOrWhiteSpace(file.stationType) ||
                string.IsNullOrWhiteSpace(file.stationSerial) ||
                string.IsNullOrWhiteSpace(file.modificationDate))
            {
                throw new InvalidDataException("Svantek project file row contains an empty required field.");
            }

            _ = file.measurementPointId;
            _ = file.fileSize;
            _ = file.status;
            _ = file.index;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("Svantek project file row contains malformed field data.", exception);
        }
    }
}
