// File summary: Defines reusable query, filter, ordering, and result models for searchable grids.
// Major updates:
// - 2026-07-30 pending Removed the never-failing WasSuccessful/ErrorMessage pair and the consumerless IOperationResult abstraction.
// - 2026-06-10 pending Moved IOperationResult into its own source file for Sonar maintainability.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

namespace RVT.Entities.Querying;

public class SearchQueryResult<T>
{
    // Function summary: Initializes this type with the dependencies required by its workflow.
    public SearchQueryResult()
    {
        Value = new List<T>();
        AdditionalInfo = string.Empty;
    }

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public SearchQueryResult(IList<T> value, int recordCount, string additionalInfo)
    {
        Value = [.. value];
        RecordCount = recordCount;
        AdditionalInfo = additionalInfo;
    }

    public List<T> Value { get; set; }
    public int RecordCount { get; set; }
    public string AdditionalInfo { get; set; }

    /// <summary>
    /// True when an unpaged read hit its <c>maximumRecords</c> bound and rows were left unread.
    /// Lets callers distinguish a capped result from a complete one instead of silently truncating.
    /// </summary>
    public bool HasMore { get; set; }
}
