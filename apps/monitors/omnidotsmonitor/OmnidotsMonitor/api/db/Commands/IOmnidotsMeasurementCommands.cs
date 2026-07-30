using Omnidots.Model.Json;

namespace Omnidots.Api.Db;

public interface IOmnidotsMeasurementCommands
{
    void WriteTraces(string serialId, IReadOnlyList<TraceData> traces);
}
