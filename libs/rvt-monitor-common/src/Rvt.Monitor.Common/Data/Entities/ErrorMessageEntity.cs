namespace Rvt.Monitor.Common.Data.Entities;

public sealed class ErrorMessageEntity
{
    public string Host { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? Variables { get; set; }
    public DateTime LogTime { get; set; }
}
