using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Omnidots.Api;
using Omnidots.Api.Db;
using Omnidots.Api.Db.EntityFramework;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Rvt.Monitor.Common.Alerts;
using Rvt.Monitor.Common.Alerts.Persistence;
using Rvt.Monitor.Common.Data;
using Rvt.Monitor.Common.Data.EntityFramework;
using Rvt.Monitor.Common.Hosting;
using Rvt.Monitor.Common.Mqtt;

namespace OmnidotsMonitorTests.Architecture;

[TestClass]
[DoNotParallelize]
public sealed class OmnidotsAlertArchitectureTests
{
    [TestMethod]
    public void AddOmnidotsMonitor_ResolvesFocusedDurableAlertComposition()
    {
        IConfiguration configuration = Configuration(apiEnabled: true, validSecurity: true);
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(new MonitorExecutionModeContext(MonitorExecutionMode.Api));
        services.AddLogging();
        services.AddOmnidotsMonitor(configuration);
        services.PostConfigure<DurableAlertOptions>(options =>
            options.PortalBaseUrl = "https://portal.example.test/");

        ServiceDescriptor? productionFactoryRegistration = services.SingleOrDefault(service =>
            service.ServiceType == typeof(IMonitorDbContextFactory<OmnidotsMonitorContext>));
        Assert.IsNotNull(productionFactoryRegistration);
        services.Replace(ServiceDescriptor.Singleton<IMonitorDbContextFactory<OmnidotsMonitorContext>>(
            new OmnidotsMonitorContextFactory(
                "Host=localhost;Database=composition;Username=composition;Password=composition",
                new MonitorDbOptions(new Dictionary<string, string>()))));

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.IsInstanceOfType<ProcessWebhookHandler>(provider.GetRequiredService<ProcessWebhookHandler>());
        Assert.IsInstanceOfType<ConfigureMeasuringPointHandler>(
            provider.GetRequiredService<ConfigureMeasuringPointHandler>());
        Assert.IsInstanceOfType<DurableAlertService>(provider.GetRequiredService<IAlertIngressPort>());
        Assert.IsInstanceOfType<EfAlertCommitStore<OmnidotsMonitorContext>>(
            provider.GetRequiredService<IAlertCommitStore>());
        Assert.IsInstanceOfType<EfAlertOutboxStore<OmnidotsMonitorContext>>(
            provider.GetRequiredService<IAlertOutboxStore>());
        Assert.IsInstanceOfType<MonitorEventPublisher>(provider.GetRequiredService<IMonitorEventPublisher>());
        Assert.IsInstanceOfType<OmnidotsMonitorContextFactory>(
            provider.GetRequiredService<IMonitorDbContextFactory<OmnidotsMonitorContext>>());
        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(MqttAlertDeliveryAdapter),
                typeof(EmailAlertDeliveryAdapter),
                typeof(SmsAlertDeliveryAdapter)
            },
            provider.GetServices<IAlertDeliveryAdapter>().Select(adapter => adapter.GetType()).ToArray());
    }

    [TestMethod]
    public async Task ApiDisabled_InvalidApiSecrets_DoNotFailHostStartup()
    {
        using IHost host = CreateHost(apiEnabled: false, validSecurity: false);

        await host.StartAsync();
        await host.StopAsync();
    }

    [TestMethod]
    public async Task ApiEnabled_InvalidApiSecrets_FailHostStartup()
    {
        using IHost host = CreateHost(apiEnabled: true, validSecurity: false);

        OptionsValidationException exception = await Assert.ThrowsExactlyAsync<OptionsValidationException>(
            () => host.StartAsync());

        Assert.AreEqual(Options.DefaultName, exception.OptionsName);
        Assert.Contains(OmnidotsApiSecurityValidation.FailureMessage, exception.Failures);
    }

    [TestMethod]
    public void WebhookBoundary_HasNoLegacyStringOrFacadeSurface()
    {
        MethodInfo[] facadeMethods = typeof(OmnidotsApi).GetMethods()
            .Concat(typeof(OmnidotsService).GetMethods())
            .Where(method => method.Name.Contains("Webhook", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("ConfigureMeasuringPoint", StringComparison.Ordinal))
            .ToArray();

        Assert.IsEmpty(facadeMethods);
        Assert.IsNull(typeof(OmnidotsApi).Assembly.GetType("Omnidots.Api.UseCases.LegacyProcessWebhookHandler"));
        Assert.IsNull(typeof(OmnidotsApi).Assembly.GetType("Omnidots.Model.Config.OmnidotsWebhookOptions"));

        MethodInfo[] endpointMethods = typeof(MonitorApiEndpoints).GetMethods(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(method => method.Name is "Webhook" or "ConfigureMeasuringPoint")
            .ToArray();
        Assert.HasCount(2, endpointMethods);
        Assert.IsTrue(endpointMethods.All(method =>
            method.GetParameters().All(parameter => parameter.ParameterType != typeof(OmnidotsService))));
        Assert.IsTrue(endpointMethods.Single(method => method.Name == "Webhook")
            .GetParameters().Any(parameter => parameter.ParameterType == typeof(ProcessWebhookHandler)));
        Assert.IsTrue(endpointMethods.Single(method => method.Name == "ConfigureMeasuringPoint")
            .GetParameters().Any(parameter => parameter.ParameterType == typeof(ConfigureMeasuringPointHandler)));

        string endpointSource = ReadSource("omnidotsmonitor/OmnidotsMonitor/api/MonitorApiEndpoints.cs");
        Assert.DoesNotContain("[FromServices] OmnidotsService", endpointSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessWebhook(string", ReadSource(
            "omnidotsmonitor/OmnidotsMonitor/api/OmnidotsApi.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("Webhook(string", ReadSource(
            "omnidotsmonitor/OmnidotsMonitor/api/OmnidotsService.cs"), StringComparison.Ordinal);
    }

    [TestMethod]
    public void NewAlertSlice_DependsOnlyOnFocusedCommonPorts()
    {
        Type[] constructorDependencies = typeof(ProcessWebhookHandler).GetConstructors().Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        CollectionAssert.DoesNotContain(constructorDependencies, typeof(IDBClient));
        CollectionAssert.DoesNotContain(constructorDependencies, typeof(OmnidotsRuleProcessor));
        Assert.IsTrue(constructorDependencies.All(type =>
            type.FullName is not "Rvt.Communication.Abstractions.IMessageService" and
            not "Rvt.Monitor.Common.Mqtt.IMqttClient"));

        string processSource = ReadSource("omnidotsmonitor/OmnidotsMonitor/api/UseCases/ProcessWebhookHandler.cs");
        Assert.DoesNotContain("IDBClient", processSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OmnidotsRuleProcessor", processSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IMessageService", processSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IMqttClient", processSource, StringComparison.Ordinal);

        AssemblyName[] commonReferences = typeof(IAlertIngressPort).Assembly.GetReferencedAssemblies();
        Assert.IsFalse(commonReferences.Any(reference =>
            reference.Name?.Contains("Omnidots", StringComparison.OrdinalIgnoreCase) == true));
        Assert.IsFalse(typeof(IAlertIngressPort).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Rvt.Monitor.Common.Alerts", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IDBClient)));
    }

    private static IHost CreateHost(bool apiEnabled, bool validSecurity)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--hostBuilder:reloadConfigOnChange=false"]);
        builder.Configuration.AddConfiguration(Configuration(apiEnabled, validSecurity));
        builder.Services.AddSingleton(new MonitorExecutionModeContext(
            apiEnabled ? MonitorExecutionMode.Api : MonitorExecutionMode.QuartzScheduler));
        builder.Services.AddOmnidotsMonitor(builder.Configuration);
        builder.Services.PostConfigure<DurableAlertOptions>(options =>
            options.PortalBaseUrl = "https://portal.example.test/");
        builder.Services.Replace(ServiceDescriptor.Singleton<IMonitorDbContextFactory<OmnidotsMonitorContext>>(
            new OmnidotsMonitorContextFactory(
                "Host=localhost;Database=composition;Username=composition;Password=composition",
                new MonitorDbOptions(new Dictionary<string, string>()))));
        return builder.Build();
    }

    private static IConfiguration Configuration(bool apiEnabled, bool validSecurity)
    {
        Dictionary<string, string?> values = new Dictionary<string, string?>
        {
            ["MonitorApi:Enabled"] = apiEnabled.ToString(),
            ["Infrastructure"] = "local",
            ["MonitorScheduler:Enabled"] = "true",
            ["RVT:EMAIL_ENABLED"] = "false",
            ["RVT:SMS_ENABLED"] = "false",
            [$"{OmnidotsMonitoringOptions.SectionName}:Recipient"] = "monitoring@example.test",
            [$"{OmnidotsMonitoringOptions.SectionName}:TimeZoneId"] = "Europe/London",
            [$"{OmnidotsMonitoringOptions.SectionName}:WindowStart"] = "08:30:00",
            [$"{OmnidotsMonitoringOptions.SectionName}:WindowEnd"] = "18:00:00",
            [$"{OmnidotsMonitoringOptions.SectionName}:StaleAfter"] = "01:00:00",
            [$"{OmnidotsTraceCollectionOptions.SectionName}:Enabled"] = "false",
            [$"{OmnidotsTraceCollectionOptions.SectionName}:MaxMonitorsPerRun"] = "1",
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookUrl"] = "https://alerts.example.test/omnidots",
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookSecret"] = validSecurity
                ? "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww"
                : "short",
            [$"{OmnidotsApiSecurityOptions.SectionName}:ConfigSecret"] = validSecurity
                ? "cccccccccccccccccccccccccccccccc"
                : "short",
            [$"{OmnidotsApiSecurityOptions.SectionName}:NotificationDelayMinutes"] = "5",
            [$"{OmnidotsApiSecurityOptions.SectionName}:WebhookConcurrencyLimit"] = "8",
            [$"{OmnidotsApiSecurityOptions.SectionName}:ConfigureConcurrencyLimit"] = "2",
            [$"{DurableAlertOptions.SectionName}:PortalBaseUrl"] = "https://portal.example.test/"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static string ReadSource(string relativePath)
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "rvt-monitors.sln")))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "Could not locate the repository root from the test output directory.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
