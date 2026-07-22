using System.Collections.Immutable;

namespace Svantek.Api;

public sealed class SvantekJobAggregateException : Exception
{
    public SvantekJobAggregateException(string jobName, IEnumerable<Exception> failures)
        : base($"Svantek job '{jobName}' failed for one or more independent units.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentNullException.ThrowIfNull(failures);

        JobName = jobName;
        Failures = failures.ToImmutableArray();
        if (Failures.IsEmpty)
        {
            throw new ArgumentException("At least one failure is required.", nameof(failures));
        }
    }

    public string JobName { get; }

    public ImmutableArray<Exception> Failures { get; }
}
