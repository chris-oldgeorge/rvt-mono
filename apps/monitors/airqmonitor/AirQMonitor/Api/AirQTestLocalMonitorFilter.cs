using AirQ.Model.Dto;
using AirQ.Model.Http;

namespace AirQ.Api;

public sealed class AirQTestLocalMonitorFilter
{
    private readonly string? _targetSerialId;

    private AirQTestLocalMonitorFilter(string? targetSerialId) => this._targetSerialId = targetSerialId;

    public static AirQTestLocalMonitorFilter Create(bool enabled, string? targetSerialId)
    {
        if (enabled && string.IsNullOrWhiteSpace(targetSerialId))
        {
            throw new InvalidOperationException(
                "AirQ testlocal requires AirQ__TestLocal__SerialId to identify one monitor.");
        }

        return new AirQTestLocalMonitorFilter(enabled ? targetSerialId : null);
    }

    public List<NoiseMonitorDto> Apply(List<NoiseMonitorDto> monitors) =>
        _targetSerialId is null
            ? monitors
            : [.. monitors.Where(monitor => IsTargetSerial(monitor.SerialId))];

    public List<InstrumentResponse> ApplyCatalog(List<InstrumentResponse> monitors) =>
        _targetSerialId is null
            ? monitors
            : [.. monitors.Where(monitor => IsTargetSerial(monitor.InstrumentID))];

    private bool IsTargetSerial(string? serialId) =>
        string.Equals(serialId, _targetSerialId, StringComparison.Ordinal);
}
