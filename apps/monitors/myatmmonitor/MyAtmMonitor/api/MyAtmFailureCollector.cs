using System.Runtime.ExceptionServices;
using MyAtm.Api.Db;

namespace MyAtm.Api;

public sealed class MyAtmFailureCollector
{
    private readonly IMyAtmOperationalCommands operationalCommands;
    private readonly List<MyAtmJobFailure> failures = [];

    public MyAtmFailureCollector(IMyAtmOperationalCommands operationalCommands)
    {
        this.operationalCommands = operationalCommands;
    }

    public void Capture(
        string identifier,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        try
        {
            operationalCommands.HandleException(identifier, exception);
            failures.Add(new MyAtmJobFailure(identifier, exception));
        }
        catch (Exception recordingException)
        {
            failures.Add(new MyAtmJobFailure(identifier, exception, recordingException));
        }
    }

    public void ThrowIfAny(string operation)
    {
        if (failures.Count > 0)
        {
            throw new MyAtmJobAggregateException(operation, failures);
        }
    }
}
