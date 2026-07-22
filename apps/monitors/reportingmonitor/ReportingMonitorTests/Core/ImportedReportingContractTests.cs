using Rvt.Reporting.Core.Models;
using Rvt.Reporting.Core.Reports;
using Rvt.Reporting.Core.Scheduling;

namespace ReportingMonitorTests.Core;

public sealed class ImportedReportingContractTests
{
    [Fact]
    public void ImportedReportingDomain_ExposesExpectedContractsAndDailyScheduling()
    {
        Assert.True(typeof(IReportGenerationService).IsInterface);
        Assert.True(typeof(IReportPdfRenderer).IsInterface);
        Assert.True(typeof(IReportStorage).IsInterface);
        Assert.True(typeof(IReportMessageSender).IsInterface);
        Assert.True(typeof(ICustomerLogoProvider).IsInterface);
        Assert.True(typeof(IReportNarrativeProvider).IsInterface);
        Assert.Equal(FrequencyType.OneTime, (FrequencyType)5);

        var periods = ReportPeriodCalculator.CreatePeriods(
            new ReportRule { Frequency = FrequencyType.Daily },
            new DateTimeOffset(2026, 7, 14, 4, 0, 0, TimeSpan.Zero));

        Assert.Single(periods);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero), periods[0].StartUtc);
    }
}
