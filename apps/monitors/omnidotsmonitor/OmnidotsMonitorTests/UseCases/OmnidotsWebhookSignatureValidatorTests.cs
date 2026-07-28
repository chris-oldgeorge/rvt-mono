using System.Security.Cryptography;
using System.Text;
using Omnidots.Api.UseCases;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class OmnidotsWebhookSignatureValidatorTests
{
    private const string Body = "{\"alarm\":\"exact body\"}";
    private const string Secret = "webhook-secret-that-is-at-least-32-bytes";
    private readonly OmnidotsWebhookSignatureValidator validator = new();

    [TestMethod]
    public void IsValid_AcceptsExactSha256Signature()
    {
        Assert.IsTrue(validator.IsValid(Body, ComputeSignature(Body, Secret), Secret));
    }

    [TestMethod]
    public void IsValid_AcceptsSignatureOverExactBodyBytes()
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(Body);
        byte[] body = Encoding.UTF8.GetPreamble().Concat(jsonBytes).ToArray();

        Assert.IsTrue(validator.IsValid(body, ComputeSignature(body, Secret), Secret));
    }

    [TestMethod]
    public void IsValid_DoesNotNormalizeInvalidUtf8BodyBytes()
    {
        byte[] body = [0x7b, 0x22, 0x76, 0x22, 0x3a, 0xff, 0x7d];
        string signature = ComputeSignature(body, Secret);

        Assert.IsTrue(validator.IsValid(body, signature, Secret));

        body[5] = 0xfe;
        Assert.IsFalse(validator.IsValid(body, signature, Secret));
    }

    [DataRow(null)]
    [DataRow("")]
    [DataRow("sha256=")]
    [DataRow("SHA256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [DataRow(" sha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [DataRow("sha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [DataRow("sha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [DataRow("sha256=gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [TestMethod]
    public void IsValid_RejectsMissingOrMalformedSignature(string? signature)
    {
        Assert.IsFalse(validator.IsValid(Body, signature, Secret));
    }

    [TestMethod]
    public void IsValid_RejectsDigestForDifferentBodyOrSecret()
    {
        string signature = ComputeSignature(Body, Secret);

        Assert.IsFalse(validator.IsValid(Body + " ", signature, Secret));
        Assert.IsFalse(validator.IsValid(Body, signature, Secret + "-different"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("short-webhook-secret")]
    public void IsValid_RejectsUnusableConfiguredSecretEvenForMatchingHmac(string secret)
    {
        string signature = ComputeSignature(Body, secret);

        Assert.IsFalse(validator.IsValid(Body, signature, secret));
    }

    [TestMethod]
    public void IsValid_AcceptsSecretWithThirtyTwoUtf8Bytes()
    {
        string secret = string.Concat(Enumerable.Repeat("é", 16));

        Assert.IsTrue(validator.IsValid(Body, ComputeSignature(Body, secret), secret));
    }

    [TestMethod]
    public void IsValid_RejectsSecretWithInvalidUnicodeEvenForMatchingReplacementHmac()
    {
        string secret = new string('\ud800', 1) + new string('w', 32);
        string signature = ComputeSignature(Body, secret);

        Assert.IsFalse(validator.IsValid(Body, signature, secret));
    }

    private static string ComputeSignature(string body, string secret)
    {
        byte[] digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return $"sha256={Convert.ToHexStringLower(digest)}";
    }

    private static string ComputeSignature(byte[] body, string secret)
    {
        byte[] digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return $"sha256={Convert.ToHexStringLower(digest)}";
    }
}
