using Rvt.Monitor.Common.Notifications;
using Rvt.Monitor.Common.Rules;

using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;

namespace Omnidots.Api.Db;

public interface IOmnidotsRuleQueries
{
    List<RvtAlertRuleDto> ReadRules(string? serialId);

    List<RvtContactDto> ReadAlertContacts(Guid monitorId);

    List<NotificationDto> ReadNotifications(Guid monitorId, DateTime after);

    double GetAveragePeakLevels(string serialId, string columnName, DateTime start, DateTime end);
}
