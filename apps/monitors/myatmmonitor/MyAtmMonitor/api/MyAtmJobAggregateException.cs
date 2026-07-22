namespace MyAtm.Api;

public sealed record MyAtmJobFailure(
    string Identifier,
    Exception Exception,
    Exception? RecordingException = null);

public sealed class MyAtmJobAggregateException : Exception
{
    public MyAtmJobAggregateException(string operation, IReadOnlyList<MyAtmJobFailure> failures)
        : base($"{operation} failed for {failures.Count} item(s): {string.Join(", ", failures.Select(failure => failure.Identifier))}")
    {
        Operation = operation;
        Failures = failures.ToArray();
    }

    public string Operation { get; }

    public IReadOnlyList<MyAtmJobFailure> Failures { get; }
}
