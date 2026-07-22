// File summary: Signals a deployment failure that should be reported to the operator rather than thrown as a crash.
// Major updates:
// - 2026-07-14 pending Added to replace the post-load half of the retired RVT.DatabaseMigrator.

namespace RVT.SchemaDeploy;

public sealed class DeployException : Exception
{
    // Function summary: Initializes this type with the dependencies required by its workflow.
    public DeployException()
    {
    }

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public DeployException(string message)
        : base(message)
    {
    }

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public DeployException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
