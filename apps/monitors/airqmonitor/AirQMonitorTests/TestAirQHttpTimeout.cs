using AirQ.Api.Http;

namespace AirQMonitorTests;

/// <summary>
/// The vendor HTTP client previously left <see cref="HttpClient.Timeout"/>
/// at the 100 second default, so an unresponsive AirQ endpoint stalled the
/// whole fleet import for that long on every request.
/// </summary>
[TestClass]
public class TestAirQHttpTimeout
{
    [TestMethod]
    public void HttpWebClient_AppliesAnExplicitBoundedRequestTimeout()
    {
        using HttpClient inner = new();

        _ = new HttpWebClient("https://airq.example.test", inner);

        Assert.AreEqual(HttpWebClient.RequestTimeout, inner.Timeout);
    }

    [TestMethod]
    public void HttpWebClient_RequestTimeoutIsShorterThanTheFrameworkDefault()
    {
        Assert.IsTrue(
            HttpWebClient.RequestTimeout < TimeSpan.FromSeconds(100),
            "The vendor timeout must be tighter than the 100 second framework default.");
        Assert.IsTrue(
            HttpWebClient.RequestTimeout > TimeSpan.Zero,
            "The vendor timeout must be a positive duration.");
    }

    [TestMethod]
    public void HttpWebClient_SetsTheVendorBaseAddressAndAcceptHeader()
    {
        using HttpClient inner = new();

        _ = new HttpWebClient("https://airq.example.test", inner);

        Assert.AreEqual(new Uri("https://airq.example.test"), inner.BaseAddress);
        Assert.IsTrue(inner.DefaultRequestHeaders.Accept.Count > 0
            || inner.DefaultRequestHeaders.Contains("accept"));
    }
}
