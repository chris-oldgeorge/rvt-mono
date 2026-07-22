// File summary: Defines reusable query, filter, ordering, and result models for searchable grids.
// Major updates:
// - 2026-06-10 pending Moved IOperationResult into its own source file for Sonar maintainability.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

namespace RVT.Entities.Querying;

public class SearchQueryResult<T> : IOperationResult
{
    // Function summary: Initializes this type with the dependencies required by its workflow.
    public SearchQueryResult()
    {
        Value = new List<T>();
        ErrorMessage = string.Empty;
        AdditionalInfo = string.Empty;
    }

    // Function summary: Initializes this type with the dependencies required by its workflow.
    public SearchQueryResult(bool wasSuccessful, string errorMessage, IList<T> value, int recordCount, string additionalInfo)
    {
        WasSuccessful = wasSuccessful;
        ErrorMessage = errorMessage;
        Value = value.ToList();
        RecordCount = recordCount;
        AdditionalInfo = additionalInfo;
    }

    public bool WasSuccessful { get; set; }
    public string ErrorMessage { get; set; }
    public List<T> Value { get; set; }
    public int RecordCount { get; set; }
    public string AdditionalInfo { get; set; }

    /// <summary>
    /// True when an unpaged read hit its <c>maximumRecords</c> bound and rows were left unread.
    /// Lets callers distinguish a capped result from a complete one instead of silently truncating.
    /// </summary>
    public bool HasMore { get; set; }
}
