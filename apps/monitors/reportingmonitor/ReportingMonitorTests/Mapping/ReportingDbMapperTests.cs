using ReportingMonitor.Api.Db.EntityFramework;
using Rvt.Reporting.Core.Models;

namespace ReportingMonitorTests.Mapping;

public sealed class ReportingDbMapperTests
{
    [Fact]
    public void ToReportRule_MapsScalarRuleValuesAndRecipients()
    {
        var entity = new ReportRuleEntity
        {
            Id = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Frequency = (int)FrequencyType.Daily,
            IsHiddenSystemRule = false
        };

        var result = ReportingDbMapper.ToReportRule(entity, [new ReportRecipient(entity.UserId, "report@example.com")]);

        Assert.Equal(entity.Id, result.Id);
        Assert.Equal(entity.SiteId, result.SiteId);
        Assert.Equal(entity.UserId, result.UserId);
        Assert.Equal(FrequencyType.Daily, result.Frequency);
        Assert.Equal("report@example.com", Assert.Single(result.Recipients).Email);
    }
}
