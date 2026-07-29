using System.Runtime.ExceptionServices;
using Svantek.Api.Db;

namespace Svantek.Api;

public sealed class SvantekFailureCollector(ISvantekOperationalCommands operationalCommands)
{
    private readonly ISvantekOperationalCommands _operationalCommands = operationalCommands;
    private readonly List<Exception> _failures = [];

    public void Capture(string identifier, Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        _operationalCommands.HandleException(identifier, exception);
        _failures.Add(new InvalidOperationException(identifier, exception));
    }

    public void ThrowIfAny(string jobName)
    {
        if (_failures.Count > 0)
        {
            throw new SvantekJobAggregateException(jobName, _failures);
        }
    }
}
