using System.Data;
using Omnidots.Model.Dto;
using Omnidots.Model.Json;

namespace Omnidots.Api.Db;

public interface IOmnidotsMeasurementCommands
{
    void InsertPeakRecords(string serialId, List<PeakRecordDto> dtos);

    void InsertPeakRecordsTable(DataTable table);

    void InsertVeffRecords(string serialId, List<VeffRecordDto> dtos);

    void InsertVdvRecords(string serialId, List<VdvRecordDto> dtos);

    void WriteTraces(string serialId, IReadOnlyList<TraceData> traces);
}
