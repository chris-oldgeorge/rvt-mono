namespace Omnidots.Api.Db;

public interface IOmnidotsImportCursorQueries
{
    DateTime? ReadImportCursor(string serialId, OmnidotsMeasurementSeries series);

    DateTime? ReadLatestMeasurementTime(string serialId, OmnidotsMeasurementSeries series);
}
