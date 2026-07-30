using System.Globalization;
using Rvt.Storage;
using Svantek.Api.Db;
using Svantek.Api.Ports;
using Svantek.Api.Storage;
using Svantek.Model.Http;
using Svantek.Model.Dto;

namespace Svantek.Api.UseCases;

// Summary: Checks Svantek alert notifications for matching audio recordings and uploads them to blob storage.
public sealed class CheckForSoundRecordingsHandler
{
    private readonly ISvantekNotificationQueries _notificationQueries;
    private readonly ISvantekOperationalCommands _operationalCommands;
    private readonly ISvantekVendorGateway _gateway;
    private readonly IObjectStorageClient _storage;

    public CheckForSoundRecordingsHandler(
        ISvantekNotificationQueries notificationQueries,
        ISvantekOperationalCommands operationalCommands,
        ISvantekVendorGateway gateway,
        IObjectStorageClientFactory storageFactory)
    {
        _notificationQueries = notificationQueries;
        _operationalCommands = operationalCommands;
        _gateway = gateway;
        ArgumentNullException.ThrowIfNull(storageFactory);
        _storage = storageFactory.GetRequiredClient(
            SvantekStorageComposition.SoundRecordingsResource);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, List<ProjectFile>> filesCache = [];
        List<NoiseNotificationLatest> alerts = await _notificationQueries
            .ReadLatestNotificationAsync(cancellationToken)
            .ConfigureAwait(false);
        SvantekFailureCollector failures = new(_operationalCommands);

        foreach (NoiseNotificationLatest alert in alerts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string dayCode = alert.NotificationTime.AddMinutes(-1).ToString("yyyyMMdd");
                List<ProjectFile> audioFiles = await FindFilesAsync(
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

                ProjectFile audioFile = audioFiles.Count == 1
                    ? audioFiles[0]
                    : audioFiles.MinBy(file => Math.Abs((TriggerDate(file) - alert.NotificationTime).TotalSeconds))!;
                byte[] content = await _gateway.GetSoundFileAsync(
                    alert.ProjectId,
                    alert.PointId,
                    audioFile.stationType,
                    dayCode,
                    audioFile.stationSerial,
                    audioFile.filename,
                    cancellationToken).ConfigureAwait(false);
                string fileName = $"{alert.NotificationId}.wav";
                await using MemoryStream stream = new(content, writable: false);
                await _storage.WriteAsync(
                    new StorageWriteRequest(
                        StorageObjectKey.Parse(fileName),
                        stream,
                        "audio/wav"),
                    cancellationToken).ConfigureAwait(false);
                await _operationalCommands.WriteSoundFileAsync(
                    alert.NotificationId,
                    fileName,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Capture(
                    $"sound:{alert.NotificationId}",
                    exception,
                    cancellationToken);
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
        List<ProjectFile> files = await FetchFilesAsync(
            filesCache,
            dayCode,
            projectId,
            pointId,
            cancellationToken).ConfigureAwait(false);
        return [.. files
            .Where(file =>
            {
                DateTime triggerDate = TriggerDate(file);
                return triggerDate >= alertTime.AddSeconds(-averagePeriod) &&
                       triggerDate <= alertTime;
            })];
    }

    private async Task<List<ProjectFile>> FetchFilesAsync(
        Dictionary<string, List<ProjectFile>> filesCache,
        string dayCode,
        int projectId,
        int pointId,
        CancellationToken cancellationToken)
    {
        string listId = $"{projectId}:{pointId}:{dayCode}";
        if (filesCache.TryGetValue(listId, out List<ProjectFile>? cached))
        {
            return cached;
        }

        List<ProjectFile> files = await _gateway.GetProjectFilesAsync(
            projectId.ToString(),
            pointId.ToString(),
            dayCode,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (ProjectFile file in files)
        {
            ValidateFileRow(file);
        }

        List<ProjectFile> soundFiles = [.. files.Where(file => file.filename.Contains(".WAV", StringComparison.Ordinal))];
        filesCache.Add(listId, soundFiles);
        return soundFiles;
    }

    // Derives the trigger timestamp from the sound file's name
    // (yyyyMMdd_HHmmss...). A name that cannot yield one is malformed vendor
    // data, so this fails explicitly - the per-alert failure collector records
    // it, matching ValidateFileRow - instead of the old silent local
    // DateTime.Now fallback that quietly excluded the file from matching.
    private static DateTime TriggerDate(ProjectFile file)
    {
        string filename = file.filename;
        if (filename.Length < 17)
        {
            throw new InvalidDataException(
                $"Svantek sound file name '{filename}' is too short to contain a trigger timestamp.");
        }

        string filenameTime =
            filename[..4] + "-" + filename[4..6] + "-" + filename[6..8] + " " +
            filename[9..11] + ":" + filename[12..14] + ":" + filename[15..17];
        if (!DateTime.TryParse(filenameTime, CultureInfo.InvariantCulture, out DateTime triggerDate))
        {
            throw new InvalidDataException(
                $"Svantek sound file name '{filename}' does not contain a parseable trigger timestamp.");
        }

        return triggerDate;
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
