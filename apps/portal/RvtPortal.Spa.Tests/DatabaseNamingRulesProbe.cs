// File summary: Small test-side wrapper over the canonical naming rules.
// Major updates:
// - 2026-07-14 pending Added so naming tests read as intent rather than as calls into configuration internals.

using RVT.DataAccess.Configuration;

namespace RvtPortal.Spa.Tests;

internal static class DatabaseNamingRulesProbe
{
    // Function summary: Returns the canonical column name for a CLR property name.
    public static string Column(string propertyName)
    {
        return DatabaseNamingRules.ToCanonicalColumnName(propertyName);
    }
}
