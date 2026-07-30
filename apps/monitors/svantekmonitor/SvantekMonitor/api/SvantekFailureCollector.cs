using System.Runtime.ExceptionServices;
using Svantek.Api.Db;

namespace Svantek.Api;

public sealed class SvantekFailureCollector
{
    private readonly ISvantekOperationalCommands _operationalCommands;
    private readonly List<Exception> _failures = [];

    public SvantekFailureCollector(ISvantekOperationalCommands operationalCommands)
    {
        _operationalCommands = operationalCommands;
    }

    public void Capture(
        string identifier,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        // Only a cancelled run token means "stop the fleet": an HttpClient
        // timeout also surfaces as a TaskCanceledException and must be
        // recorded as an ordinary per-unit failure (MyAtm's semantics).
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        try
        {
            _operationalCommands.HandleException(identifier, exception);
            _failures.Add(new InvalidOperationException(identifier, exception));
        }
        catch (Exception recordingException)
        {
            // Recording is best-effort: a database outage while writing the
            // error row must not replace or swallow the original failure.
            _failures.Add(new InvalidOperationException(
                identifier,
                new AggregateException(exception, recordingException)));
        }
    }

    public void ThrowIfAny(string jobName)
    {
        if (_failures.Count > 0)
        {
            throw new SvantekJobAggregateException(jobName, _failures);
        }
    }
}
