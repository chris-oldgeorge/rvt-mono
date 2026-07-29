using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace Svantek.Api.Db;

public interface ISvantekRuleQueries
{
    List<RvtAlertRuleDto> ReadRules(string? serialNumber);

    List<RvtContactDto> ReadAlertContacts(Guid monitorId, out Guid siteId);

    bool HasOpenNotification(Guid monitorId, string alertField, AlertType alertType);

    double GetAverageNoiseLevel(string serialNumber, string columnName, DateTime start, DateTime end);
}
