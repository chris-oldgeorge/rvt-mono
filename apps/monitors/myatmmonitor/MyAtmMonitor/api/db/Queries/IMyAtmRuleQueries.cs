using Rvt.Monitor.Common.Rules;

using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;

namespace MyAtm.Api.Db
{
    public interface IMyAtmRuleQueries
    {
        List<RvtAlertRuleDto> ReadRules(string? serialId);

        List<RvtAlertRuleDto> ReadRules(string? serialId, Period period);

        List<RvtAlertRuleDto> ReadRules(Period period);

        List<RvtContactDto> ReadAlertContacts(Guid monitorId);

        double? GetAverageDustLevel(string serialNumber, string columnName, DateTime start, DateTime end);
    }
}
