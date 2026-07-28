// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using Omnidots.Api.Http;

namespace OmnidotsAdapterTests
{
    /// <summary>
    /// The vendor HTTP client previously left <see cref="HttpClient.Timeout"/>
    /// at the 100 second default, so an unresponsive Omnidots endpoint stalled
    /// the vibration import for that long on every request.
    /// </summary>
    [TestClass]
    public class TestOmnidotsHttpTimeout
    {
        [TestMethod]
        public void HttpWebClient_AppliesAnExplicitBoundedRequestTimeout()
        {
            using var inner = new HttpClient();

            _ = new HttpWebClient("https://omnidots.example.test", inner);

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
    }
}
