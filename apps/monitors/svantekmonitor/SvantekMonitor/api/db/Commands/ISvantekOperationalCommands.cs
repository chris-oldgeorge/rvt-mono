using Rvt.Monitor.Common.Rules;

namespace Svantek.Api.Db;

public interface ISvantekOperationalCommands
{
    void HandleException(string message, Exception exception);

    void UpdateAlertRule(RvtAlertRuleDto dto);

    bool WriteSoundFile(Guid notificationId, string fileName);

    Task<bool> WriteSoundFileAsync(
        Guid notificationId,
        string fileName,
        CancellationToken cancellationToken = default);
}
