// File summary: xUnit fact attribute that skips a test unless a real PostgreSQL connection string is configured.
// Major updates:
// - 2026-07-15 pending Extracted so timestamp guard and unmapped-column tests share one opt-in gate.

namespace RvtPortal.Spa.Tests.Support;

/// <summary>
/// Marks a test that needs a real PostgreSQL database, pointed at by the <c>RVT_TEST_POSTGRES_CONNECTION</c>
/// environment variable. CI has no PostgreSQL, and xUnit v2 has no dynamic skip, so the decision is made here at
/// discovery: without the variable the test is reported as skipped rather than quietly passing.
/// </summary>
public sealed class RequiresPostgresFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "RVT_TEST_POSTGRES_CONNECTION";

    // Function summary: Skips the test unless a PostgreSQL connection string is configured.
    public RequiresPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Skip = $"Set {ConnectionVariable} to run this against a real PostgreSQL database.";
        }
    }
}
