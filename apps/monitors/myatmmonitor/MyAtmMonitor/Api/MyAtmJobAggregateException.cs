namespace MyAtm.Api;

public sealed record MyAtmJobFailure(
    string Identifier,
    Exception Exception,
    Exception? RecordingException = null);

public sealed class MyAtmJobAggregateException(string operation, IReadOnlyList<MyAtmJobFailure> failures) : Exception($"{operation} failed for {failures.Count} item(s): {string.Join(", ", failures.Select(failure => failure.Identifier))}")
{
    public string Operation { get; } = operation;

    public IReadOnlyList<MyAtmJobFailure> Failures { get; } = [.. failures];
}
