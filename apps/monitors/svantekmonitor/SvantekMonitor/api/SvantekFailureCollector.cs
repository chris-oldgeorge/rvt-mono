using System.Runtime.ExceptionServices;
using Svantek.Api.Db;

namespace Svantek.Api;

public sealed class SvantekFailureCollector(ISvantekOperationalCommands operationalCommands)
{
    private readonly ISvantekOperationalCommands operationalCommands = operationalCommands;
    private readonly List<Exception> failures = [];

    public void Capture(string identifier, Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        operationalCommands.HandleException(identifier, exception);
        failures.Add(new InvalidOperationException(identifier, exception));
    }

    public void ThrowIfAny(string jobName)
    {
        if (failures.Count > 0)
        {
            throw new SvantekJobAggregateException(jobName, failures);
        }
    }
}
