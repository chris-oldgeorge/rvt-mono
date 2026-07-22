using Rvt.Monitor.Common.Diagnostics;

namespace Rvt.Monitor.CommonTests.Diagnostics;

[TestClass]
public sealed class SensitiveLogRedactorTests
{
    [TestMethod]
    public void Redact_KeepsAShortPrefixAndMasksTheRemainder()
    {
        Assert.AreEqual("abcd****", SensitiveLogRedactor.Redact("abcdefgh"));
        Assert.AreEqual("abc****", SensitiveLogRedactor.Redact("abc"));
        Assert.AreEqual("(empty)", SensitiveLogRedactor.Redact(string.Empty));
    }

    [TestMethod]
    public void RedactUrl_MasksSensitiveQueryValuesAndKeepsOperationalContext()
    {
        var redacted = SensitiveLogRedactor.RedactUrl(
            "/latestData?userID=operator&token=very-secret-token&instrumentID=14768&user_auth=another-secret");

        Assert.AreEqual(
            "/latestData?userID=oper****&token=very****&instrumentID=14768&user_auth=anot****",
            redacted);
    }

    [TestMethod]
    public void RedactJson_MasksNestedSensitivePropertiesAndKeepsOtherValues()
    {
        var redacted = SensitiveLogRedactor.RedactJson(
            "{\"token\":\"very-secret-token\",\"payload\":{\"secret\":\"webhook-secret\",\"serialId\":\"14768\"}}");

        StringAssert.Contains(redacted, "\"token\":\"very****\"");
        StringAssert.Contains(redacted, "\"secret\":\"webh****\"");
        StringAssert.Contains(redacted, "\"serialId\":\"14768\"");
        Assert.IsFalse(redacted.Contains("very-secret-token", StringComparison.Ordinal));
        Assert.IsFalse(redacted.Contains("webhook-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RedactJson_MasksSensitiveAssignmentsInAnUnparseablePayload()
    {
        var redacted = SensitiveLogRedactor.RedactJson("token=unparseable-secret-payload");

        Assert.AreEqual("token=unpa****", redacted);
    }

    [TestMethod]
    public void RedactJson_PreservesAnUnstructuredOperationalError()
    {
        var redacted = SensitiveLogRedactor.RedactJson("Too many requests!");

        Assert.AreEqual("Too many requests!", redacted);
    }
}
