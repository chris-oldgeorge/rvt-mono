using System.Data;
using Omnidots.Model.Dto;

namespace Omnidots.Api.Db;

public interface IOmnidotsMeasurementImportCommands
{
    void ImportPeakRecords(string serialId, DataTable records, DateTime newestSampleAt);

    void ImportVeffRecords(
        string serialId,
        IReadOnlyCollection<VeffRecordDto> records,
        DateTime newestSampleAt);

    void ImportVdvRecords(
        string serialId,
        IReadOnlyCollection<VdvRecordDto> records,
        DateTime newestSampleAt);
}
