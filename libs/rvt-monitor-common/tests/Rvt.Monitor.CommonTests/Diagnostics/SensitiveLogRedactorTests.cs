using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
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

    [TestMethod]
    public void SensitiveAssignmentPattern_UsesAFiniteMatchTimeout()
    {
        FieldInfo? patternField = typeof(SensitiveLogRedactor).GetField(
            "SensitiveAssignmentPattern",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(patternField);
        var pattern = patternField.GetValue(null) as Regex;

        Assert.IsNotNull(pattern);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), pattern.MatchTimeout);
    }

    [TestMethod]
    public void RedactSensitiveAssignments_FallsBackToRedactingTheWholePayloadWhenRegexTimesOut()
    {
        MethodInfo? method = typeof(SensitiveLogRedactor).GetMethod(
            "RedactSensitiveAssignments",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(string), typeof(Regex)],
            modifiers: null);
        Regex timeoutPattern = new("(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        string payload = new string('a', 20_000) + "!";

        Assert.IsNotNull(method);
        string? redacted = method.Invoke(null, [payload, timeoutPattern]) as string;

        Assert.AreEqual(SensitiveLogRedactor.Redact(payload), redacted);
    }

    [TestMethod]
    public void RedactJson_CompletesPromptlyAndDoesNotExposeLargeMalformedSensitiveAssignments()
    {
        string payload = "token=" + new string('a', 250_000) + new string(',', 10_000);
        var started = Stopwatch.StartNew();

        string redacted = SensitiveLogRedactor.RedactJson(payload);

        started.Stop();
        Assert.IsTrue(started.Elapsed < TimeSpan.FromSeconds(2));
        Assert.IsFalse(redacted.Contains(payload[6..], StringComparison.Ordinal));
    }
}
