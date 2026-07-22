using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Rules;
using Rvt.Monitor.Common.Utilities;

namespace Rvt.Monitor.Common.Notifications
{
    public class NotificationDto
    {
        public Guid Id { get; }
        public DateTime NotificationTime { get; }
        public double LimitOn { get; }
        public int AveragingPeriod { get; }
        public double Level { get; }
        public Guid? ClosedByUser { get; }
        public DateTime? ClosedTime { get; }
        public AlertType AlertType { get; }
        public string AlertField { get; }
        public Guid MonitorId { get; }
        public string? ApiMessage { get; set; }

        public NotificationDto(Guid id, DateTime notificationTime, double limitOn, int averagingPeriod, double level,
                               DateTime? closedTime, Guid? closedByUser, AlertType alertType, string alertField, Guid monitorId)
        {
            Id = id;
            NotificationTime = notificationTime;
            LimitOn = limitOn;
            AveragingPeriod = averagingPeriod;
            Level = level;
            ClosedTime = closedTime;
            ClosedByUser = closedByUser;
            AlertType = alertType;
            AlertField = alertField;
            MonitorId = monitorId;

        }

        public NotificationDto(RvtAlertRuleDto ruleDto, double level, DateTime notificationTime, Guid monitorId)
            : this(id: Guid.NewGuid(),
                   notificationTime: notificationTime,
                   limitOn: ruleDto.LimitOn,
                   averagingPeriod: ruleDto.AveragingPeriod,
                   level: level,
                   closedTime: null,
                   closedByUser: null,
                   alertType: ruleDto.AlertType,
                   alertField: ruleDto.Field,
                   monitorId: monitorId)
        {
        }

        public string GetMessage()
        {
            var notificationUrl = "";
            if (AlertType == AlertType.Alert || AlertType == AlertType.Caution)
            {
                notificationUrl = $"{RvtConfig.PORTAL_BASE_URL}Notification/View/{Id}";
            }

            if (RvtConfig.IsOmnidotsMonitor)
            {
                return string.Format(@"Alert NotificationTime={0} AlertField={1} Level={2} LimitOn={3} AlertType={4}
                                   AveragingPeriod={5} ClosedDate={6} ClosedByUser={7}
                                   {8}",
                    DateTimeUtil.FormatString(DateTimeUtil.UtcToLocal(NotificationTime)), AlertField, Level, LimitOn, AlertType,
                    AveragingPeriod, DateTimeUtil.UtcToLocal(ClosedTime), ClosedByUser, notificationUrl);
            }

            var prefix = RvtConfig.IsNoiseMonitor
                ? AveragingPeriod == 0 ? "Noise site average" : "Noise notification"
                : "Alert";

            return string.Format(@"{0} NotificationTime={1} LimitOn={2} AlertField={3} AveragingPeriod={4}
                                   Level={5} ClosedDate={6} ClosedByUser={7} AlertType={8}
                                   {9}",
                            prefix, DateTimeUtil.UtcToLocal(NotificationTime), LimitOn, AlertField, AveragingPeriod,
                            Level, DateTimeUtil.UtcToLocal(ClosedTime), ClosedByUser, AlertType, notificationUrl);
        }
    }
}
