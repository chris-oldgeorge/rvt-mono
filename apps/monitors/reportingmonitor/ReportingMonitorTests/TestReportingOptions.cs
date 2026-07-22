using Microsoft.Extensions.Configuration;
using ReportingMonitor.Api;

namespace ReportingMonitorTests;

public sealed class TestReportingOptions
{
    [Fact]
    public void Bind_BindsAiSummaryTimeoutFromRvtEnvironmentVariables()
    {
        const string timeoutKey = "RVT__AI_SUMMARY_TIMEOUT_SECONDS";
        var previousTimeout = Environment.GetEnvironmentVariable(timeoutKey);

        try
        {
            Environment.SetEnvironmentVariable(timeoutKey, "8");

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var options = ReportingMonitorOptions.Bind(configuration);

            Assert.Equal(8, options.AiSummaryTimeoutSeconds);
        }
        finally
        {
            Environment.SetEnvironmentVariable(timeoutKey, previousTimeout);
        }
    }
}
