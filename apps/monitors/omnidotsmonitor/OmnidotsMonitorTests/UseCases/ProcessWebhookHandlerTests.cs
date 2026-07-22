using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Omnidots.Api.UseCases;
using Omnidots.Model.Config;
using Rvt.Monitor.Common.Alerts;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class ProcessWebhookHandlerTests
{
    private const string WebhookSecret = "wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww";

    [TestMethod]
    public void PublicSurface_ContainsOnlyByteRunAsyncAndFocusedDependencies()
    {
        var methods = typeof(ProcessWebhookHandler).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var constructors = typeof(ProcessWebhookHandler).GetConstructors();

        Assert.HasCount(1, methods);
        Assert.AreEqual(nameof(ProcessWebhookHandler.RunAsync), methods[0].Name);
        Assert.AreEqual(typeof(ReadOnlyMemory<byte>), methods[0].GetParameters()[0].ParameterType);
        Assert.HasCount(1, constructors);
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(IAlertIngressPort),
                typeof(OmnidotsAlarmTranslator),
                typeof(OmnidotsApiSecurityOptions),
                typeof(OmnidotsWebhookSignatureValidator)
            },
            constructors[0].GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatesOriginalBytesAndPassesExactDigestToIngress()
    {
        var ingress = new CapturingIngress();
        var handler = CreateHandler(ingress);
        var body = ValidBody();

        await handler.RunAsync(body, Signature(body));

        Assert.IsNotNull(ingress.Signal);
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(body)), ingress.Signal.SourceEventKey);
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatedBom_RemovesExactlyOneBomAfterAuthentication()
    {
        var ingress = new CapturingIngress();
        var handler = CreateHandler(ingress);
        var json = ValidBody();
        var body = Encoding.UTF8.GetPreamble().Concat(json).ToArray();

        await handler.RunAsync(body, Signature(body));

        Assert.IsNotNull(ingress.Signal);
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(body)), ingress.Signal.SourceEventKey);
    }

    [TestMethod]
    public async Task RunAsync_TwoAuthenticatedBoms_RemovesOnlyOneAndRejectsJson()
    {
        var ingress = new CapturingIngress();
        var handler = CreateHandler(ingress);
        var preamble = Encoding.UTF8.GetPreamble();
        var body = preamble.Concat(preamble).Concat(ValidBody()).ToArray();

        await Assert.ThrowsExactlyAsync<JsonException>(() =>
            handler.RunAsync(body, Signature(body)));

        Assert.IsNull(ingress.Signal);
    }

    [TestMethod]
    public async Task RunAsync_AuthenticatedInvalidUtf8_RejectsBeforeIngress()
    {
        var ingress = new CapturingIngress();
        var handler = CreateHandler(ingress);
        byte[] body = [0x7b, 0x22, 0x64, 0x61, 0x74, 0x61, 0x22, 0x3a, 0xff, 0x7d];

        await Assert.ThrowsExactlyAsync<JsonException>(() =>
            handler.RunAsync(body, Signature(body)));

        Assert.IsNull(ingress.Signal);
    }

    [TestMethod]
    public async Task RunAsync_MutatedBytesFailAuthenticationBeforeParsingOrIngress()
    {
        var ingress = new CapturingIngress();
        var handler = CreateHandler(ingress);
        var original = ValidBody();
        var signature = Signature(original);
        var mutated = original.ToArray();
        mutated[^2] = mutated[^2] == (byte)'3' ? (byte)'4' : (byte)'3';

        await Assert.ThrowsExactlyAsync<OmnidotsWebhookAuthenticationException>(() =>
            handler.RunAsync(mutated, signature));

        Assert.IsNull(ingress.Signal);
    }

    [TestMethod]
    public async Task RunAsync_CancellationFromIngressPropagatesWithOriginalToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var ingress = new CapturingIngress((_, token) =>
            Task.FromCanceled<AlertIngressResult>(token));
        var handler = CreateHandler(ingress);
        var body = ValidBody();

        var exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            handler.RunAsync(body, Signature(body), cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.AreEqual(cancellation.Token, ingress.CancellationToken);
    }

    [TestMethod]
    public async Task RunAsync_DurableDuplicate_ReturnsIngressResultUnchanged()
    {
        var duplicate = new AlertIngressResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AlertOccurrenceOutcome.Accepted,
            IsDuplicate: true);
        var ingress = new CapturingIngress((_, _) => Task.FromResult(duplicate));
        var handler = CreateHandler(ingress);
        var body = ValidBody();

        var result = await handler.RunAsync(body, Signature(body));

        Assert.AreSame(duplicate, result);
    }

    [TestMethod]
    public async Task RunAsync_InvalidDirectSecurityOptions_FailsClosedBeforeIngress()
    {
        var ingress = new CapturingIngress();
        var options = ValidOptions();
        options.NotificationDelayMinutes = 0;
        var handler = new ProcessWebhookHandler(
            ingress,
            new OmnidotsAlarmTranslator(),
            options,
            new OmnidotsWebhookSignatureValidator());
        var body = ValidBody();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            handler.RunAsync(body, Signature(body)));

        Assert.IsNull(ingress.Signal);
    }

    private static ProcessWebhookHandler CreateHandler(IAlertIngressPort ingress) => new(
        ingress,
        new OmnidotsAlarmTranslator(),
        ValidOptions(),
        new OmnidotsWebhookSignatureValidator());

    private static OmnidotsApiSecurityOptions ValidOptions() => new()
    {
        WebhookUrl = "https://alerts.example.test/omnidots",
        WebhookSecret = WebhookSecret,
        ConfigSecret = "cccccccccccccccccccccccccccccccc",
        NotificationDelayMinutes = 5,
        WebhookConcurrencyLimit = 8,
        ConfigureConcurrencyLimit = 2
    };

    private static byte[] ValidBody() => Encoding.UTF8.GetBytes("""
        {"created_at":1721037600,"data":{"alarms":{"alarm_level_1":30,"alarm_level_2":70,"alarm_level_3":100},"axes":{"x":{"vtop":{"value":12}},"y":{"vtop":{"value":8}},"z":{"vtop":{"value":4}}}},"measuring_point_id":23423}
        """);

    private static string Signature(ReadOnlySpan<byte> body)
    {
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), body);
        return $"sha256={Convert.ToHexStringLower(digest)}";
    }

    private sealed class CapturingIngress : IAlertIngressPort
    {
        private readonly Func<AlertSignal, CancellationToken, Task<AlertIngressResult>> accept;

        public CapturingIngress()
            : this((_, _) => Task.FromResult(new AlertIngressResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                AlertOccurrenceOutcome.Accepted,
                IsDuplicate: false)))
        {
        }

        public CapturingIngress(
            Func<AlertSignal, CancellationToken, Task<AlertIngressResult>> accept)
        {
            this.accept = accept;
        }

        public AlertSignal? Signal { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<AlertIngressResult> AcceptAsync(
            AlertSignal signal,
            CancellationToken cancellationToken = default)
        {
            Signal = signal;
            CancellationToken = cancellationToken;
            return accept(signal, cancellationToken);
        }
    }
}
