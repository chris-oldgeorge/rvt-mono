using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace AirQ.Api.Db;

public interface IAirQRuleQueries
{
    List<RvtAlertRuleDto> ReadRules(string? serialNumber);

    List<RvtContactDto> ReadAlertContacts(string serialId, out Guid siteId);

    List<RvtContactDto> ReadAlertContacts(Guid monitorId, out Guid siteId);

    bool HasOpenNotification(Guid monitorId, string alertField, AlertType alertType);

    double GetAverageNoiseLevel(string serialNumber, string columnName, DateTime start, DateTime end);
}
