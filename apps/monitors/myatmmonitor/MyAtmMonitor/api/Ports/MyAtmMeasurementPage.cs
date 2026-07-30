namespace MyAtm.Api.Ports;

public sealed record MyAtmMeasurementPage<T>(
    IReadOnlyList<T> Measurements,
    DateTime? NextCursor,
    bool HasMore);
