using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Diagnostics;

namespace Rvt.Monitor.IntegrationTesting;

// Summary: Shared monitor test helpers hoisted from the per-monitor TestUtil
// copies (review M10): fixture-file reading, the accepting IAlertIngressPort
// mock, and the test DbContext-factory registration.
public static class MonitorTestUtil
{
    public static string ReadTextFromFile(string fileName)
    {
        try
        {
            using StreamReader sr = new(fileName);
            string txt = sr.ReadToEnd();
            Console.WriteLine(txt);
            return txt;
        }
        catch (IOException e)
        {
            Console.WriteLine("The file could not be read:");
            Console.WriteLine(e.Message);
            throw AdapterException.Of("Could not read file=" + fileName, e);
        }
    }

    /// <summary>
    /// An <see cref="IAlertIngressPort"/> mock whose <c>AcceptAsync</c> reports
    /// every signal as a freshly accepted, non-duplicate occurrence.
    /// </summary>
    public static Mock<IAlertIngressPort> CreateAcceptingAlertIngress()
    {
        Mock<IAlertIngressPort> ingress = new();
        ingress
            .Setup(port => port.AcceptAsync(It.IsAny<AlertSignal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertIngressResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                AlertOccurrenceOutcome.Accepted,
                IsDuplicate: false));
        return ingress;
    }

    /// <summary>
    /// Replaces the registered <see cref="IMonitorDbContextFactory{TContext}"/>
    /// with the supplied test factory.
    /// </summary>
    public static void UseTestMonitorContextFactory<TContext>(
        IServiceCollection services,
        IMonitorDbContextFactory<TContext> contextFactory)
        where TContext : MonitorDbContextBase
    {
        services.Replace(
            ServiceDescriptor.Singleton<IMonitorDbContextFactory<TContext>>(contextFactory));
    }
}
