namespace Omnidots.Api.Db;

public interface IOmnidotsTraceQueries
{
    IReadOnlyDictionary<string, DateTime> ReadLatestTraceEndTimes(IReadOnlyCollection<string> serialIds);
}
