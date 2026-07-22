using Rvt.Monitor.Common.Communications;

namespace Rvt.Monitor.CommonTests.Communications;

[TestClass]
public sealed class DeliveryContractTests
{
    [TestMethod]
    public void EmailAttachment_DefensivelyCopiesContent()
    {
        var content = new byte[] { 1, 2, 3 };
        var attachment = new EmailAttachment("report.pdf", "application/pdf", content);

        content[0] = 9;

        using var stream = attachment.OpenRead();
        Assert.AreEqual(1, stream.ReadByte());
        Assert.AreEqual(3, attachment.Length);
        Assert.IsFalse(stream.CanWrite);
    }

    [DataTestMethod]
    [DataRow("", "application/pdf")]
    [DataRow(" ", "application/pdf")]
    [DataRow("report.pdf", "")]
    [DataRow("report.pdf", " ")]
    public void EmailAttachment_InvalidMetadata_Throws(string fileName, string contentType)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new EmailAttachment(fileName, contentType, new byte[] { 1 }));
    }

    [TestMethod]
    public void EmailAttachment_EmptyContent_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new EmailAttachment("report.pdf", "application/pdf", ReadOnlySpan<byte>.Empty));
    }

    [DataTestMethod]
    [DataRow("", "subject", "plain", "")]
    [DataRow(" ", "subject", "plain", "")]
    [DataRow("recipient@example.test", "", "plain", "")]
    [DataRow("recipient@example.test", " ", "plain", "")]
    [DataRow("recipient@example.test", "subject", "", "")]
    public void EmailDeliveryRequest_InvalidRequiredContent_Throws(
        string recipient,
        string subject,
        string plainTextBody,
        string htmlBody)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new EmailDeliveryRequest(
            recipient,
            subject,
            plainTextBody,
            htmlBody,
            []));
    }

    [TestMethod]
    public void EmailDeliveryRequest_HtmlOnlyBody_IsAccepted()
    {
        var request = new EmailDeliveryRequest(
            "recipient@example.test",
            "subject",
            string.Empty,
            "<p>body</p>",
            []);

        Assert.AreEqual("<p>body</p>", request.HtmlBody);
    }

    [DataTestMethod]
    [DataRow("", "content")]
    [DataRow(" ", "content")]
    [DataRow("+441234567890", "")]
    [DataRow("+441234567890", " ")]
    public void SmsDeliveryRequest_InvalidRequiredContent_Throws(string recipient, string content)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SmsDeliveryRequest(recipient, content));
    }

    [TestMethod]
    public void EmailDeliveryException_ContainsOnlySafeMetadata()
    {
        var exception = new EmailDeliveryException(
            "MicrosoftGraph",
            DeliveryFailureKind.Transient,
            "429",
            TimeSpan.FromSeconds(30));

        Assert.AreEqual("MicrosoftGraph", exception.Provider);
        Assert.AreEqual(DeliveryFailureKind.Transient, exception.FailureKind);
        Assert.AreEqual("429", exception.Code);
        Assert.AreEqual(TimeSpan.FromSeconds(30), exception.RetryAfter);
        Assert.AreEqual(
            "MicrosoftGraph email delivery failed (Transient, code 429).",
            exception.Message);
    }

    [TestMethod]
    public void SmsDeliveryException_WithoutCode_ContainsOnlySafeMetadata()
    {
        var exception = new SmsDeliveryException(
            "TransmitSMS",
            DeliveryFailureKind.Configuration);

        Assert.AreEqual(
            "TransmitSMS SMS delivery failed (Configuration).",
            exception.Message);
    }
}
