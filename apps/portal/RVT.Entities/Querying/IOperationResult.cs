// File summary: Defines shared operation-result state for query result models.
// Major updates:
// - 2026-06-10 pending Moved operation-result interface into its own source file for Sonar maintainability.

namespace RVT.Entities.Querying;

public interface IOperationResult
{
    bool WasSuccessful { get; set; }
    string ErrorMessage { get; set; }
}
