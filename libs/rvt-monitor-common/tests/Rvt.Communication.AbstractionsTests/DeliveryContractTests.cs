using Rvt.Communication.Abstractions;

namespace Rvt.Communication.AbstractionsTests;

[TestClass]
public sealed class DeliveryContractTests
{
    [TestMethod]
    public void EmailAttachment_DefensivelyCopiesContent()
    {
        byte[] content = [1, 2, 3];
        EmailAttachment attachment = new("report.pdf", "application/pdf", content);

        content[0] = 9;

        using Stream stream = attachment.OpenRead();
        Assert.AreEqual(1, stream.ReadByte());
        Assert.AreEqual(3, attachment.Length);
        Assert.IsFalse(stream.CanWrite);
    }

    [TestMethod]
    [DataRow("", "application/pdf")]
    [DataRow(" ", "application/pdf")]
    [DataRow("report.pdf", "")]
    [DataRow("report.pdf", " ")]
    public void EmailAttachment_InvalidMetadata_Throws(string fileName, string contentType)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new EmailAttachment(fileName, contentType, [1]));
    }

    [TestMethod]
    public void EmailAttachment_EmptyContent_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new EmailAttachment("report.pdf", "application/pdf", []));
    }

    [TestMethod]
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
        EmailDeliveryRequest request = new(
            "recipient@example.test",
            "subject",
            string.Empty,
            "<p>body</p>",
            []);

        Assert.AreEqual("<p>body</p>", request.HtmlBody);
    }

    [TestMethod]
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
        EmailDeliveryException exception = new(
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
        SmsDeliveryException exception = new(
            "TransmitSMS",
            DeliveryFailureKind.Configuration);

        Assert.AreEqual(
            "TransmitSMS SMS delivery failed (Configuration).",
            exception.Message);
    }
}
