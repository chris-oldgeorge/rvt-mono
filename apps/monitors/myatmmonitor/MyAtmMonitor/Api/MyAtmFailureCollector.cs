using System.Runtime.ExceptionServices;
using MyAtm.Api.Db;

namespace MyAtm.Api;

public sealed class MyAtmFailureCollector(IMyAtmOperationalCommands operationalCommands)
{
    private readonly IMyAtmOperationalCommands _operationalCommands = operationalCommands;
    private readonly List<MyAtmJobFailure> _failures = [];

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
            _operationalCommands.HandleException(identifier, exception);
            _failures.Add(new MyAtmJobFailure(identifier, exception));
        }
        catch (Exception recordingException)
        {
            _failures.Add(new MyAtmJobFailure(identifier, exception, recordingException));
        }
    }

    public void ThrowIfAny(string operation)
    {
        if (_failures.Count > 0)
        {
            throw new MyAtmJobAggregateException(operation, _failures);
        }
    }
}
