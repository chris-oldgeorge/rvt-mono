namespace ReportingMonitor.Api;

public static class ReportingMonitorJobRunner
{
    public static string? GetJobName(string[] args)
    {
        var cliJob = args.SkipWhile(arg => arg != "--job").Skip(1).FirstOrDefault();
        return string.IsNullOrWhiteSpace(cliJob)
            ? Environment.GetEnvironmentVariable("RVT__MONITOR_JOB")
            : cliJob;
    }
}
