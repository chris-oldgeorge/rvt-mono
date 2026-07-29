namespace Omnidots.Api.UseCases;

public sealed record OmnidotsMonitorFailure(
    string SerialId,
    Exception Exception,
    Exception? RecordingException = null)
{
    internal static OmnidotsMonitorFailure Record(
        string serialId,
        Exception importException,
        Action recordFailure)
    {
        try
        {
            recordFailure();
            return new OmnidotsMonitorFailure(serialId, importException);
        }
        catch (Exception recordingException)
        {
            return new OmnidotsMonitorFailure(
                serialId,
                importException,
                recordingException);
        }
    }
}

public sealed class OmnidotsImportException : Exception
{
    public string Operation { get; }
    public IReadOnlyList<OmnidotsMonitorFailure> Failures { get; }

    public OmnidotsImportException(string operation, IReadOnlyList<OmnidotsMonitorFailure> failures)
        : base($"{operation} failed for {failures.Count} monitor(s): {string.Join(", ", failures.Select(x => x.SerialId))}")
    {
        Operation = operation;
        Failures = failures;
    }
}
