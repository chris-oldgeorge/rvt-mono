using Rvt.Monitor.Common.Rules;

namespace Svantek.Api.Db;

public interface ISvantekOperationalCommands
{
    void HandleException(string message, Exception exception);

    Task ClearErrorMessagesAsync(DateTime before, CancellationToken cancellationToken = default);

    void UpdateAlertRule(RvtAlertRuleDto dto);

    Task<bool> WriteSoundFileAsync(
        Guid notificationId,
        string fileName,
        CancellationToken cancellationToken = default);
}
