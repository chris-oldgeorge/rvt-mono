using Riok.Mapperly.Abstractions;
using Rvt.Reporting.Core.Models;

namespace ReportingMonitor.Api.Db.EntityFramework;

[Mapper]
public static partial class ReportingDbMapper
{
    [MapperIgnoreSource(nameof(ReportRuleEntity.Deleted))]
    [MapperIgnoreTarget(nameof(ReportRule.Recipients))]
    [MapProperty(nameof(ReportRuleEntity.Frequency), nameof(ReportRule.Frequency))]
    private static partial ReportRule ToReportRuleValues(ReportRuleEntity source);

    public static ReportRule ToReportRule(ReportRuleEntity source, IReadOnlyList<ReportRecipient> recipients) =>
        ToReportRuleValues(source) with { Recipients = recipients };
}
