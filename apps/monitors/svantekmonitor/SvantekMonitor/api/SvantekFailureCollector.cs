using System.Runtime.ExceptionServices;
using Svantek.Api.Db;

namespace Svantek.Api;

public sealed class SvantekFailureCollector
{
    private readonly ISvantekOperationalCommands operationalCommands;
    private readonly List<Exception> failures = [];

    public SvantekFailureCollector(ISvantekOperationalCommands operationalCommands)
    {
        this.operationalCommands = operationalCommands;
    }

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
