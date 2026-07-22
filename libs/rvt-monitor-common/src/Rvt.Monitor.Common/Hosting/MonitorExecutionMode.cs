namespace Rvt.Monitor.Common.Hosting;

public enum MonitorExecutionMode
{
    Unspecified = 0,
    Api = 1,
    QuartzScheduler = 2,
    OneShot = 3
}

public sealed record MonitorExecutionModeContext(MonitorExecutionMode Mode);
