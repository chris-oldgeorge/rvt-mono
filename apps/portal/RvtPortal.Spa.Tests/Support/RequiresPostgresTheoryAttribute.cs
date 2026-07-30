// File summary: xUnit theory attribute that skips a test unless a real PostgreSQL connection string is configured.
// Major updates:
// - 2026-07-30 pending Added when the Spa workflow suites replatformed onto the Postgres integration database.

namespace RvtPortal.Spa.Tests.Support;

/// <summary>
/// The theory counterpart of <see cref="RequiresPostgresFactAttribute"/>: marks a parameterized test that
/// needs the real PostgreSQL database behind <c>RVT__POSTGRES_INTEGRATION_CONNECTION</c>. xUnit v2 has no
/// dynamic skip, so the decision is made at discovery; without the variable the theory is reported as
/// skipped rather than quietly passing.
/// </summary>
public sealed class RequiresPostgresTheoryAttribute : TheoryAttribute
{
    // Function summary: Skips the theory unless a PostgreSQL connection string is configured.
    public RequiresPostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(RequiresPostgresFactAttribute.ConnectionVariable)))
        {
            Skip = $"Set {RequiresPostgresFactAttribute.ConnectionVariable} to run this against a real PostgreSQL database.";
        }
    }
}
