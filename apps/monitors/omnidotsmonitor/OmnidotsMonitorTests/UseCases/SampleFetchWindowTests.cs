using Omnidots.Api.UseCases;

namespace OmnidotsAdapterTests.UseCases;

[TestClass]
public sealed class SampleFetchWindowTests
{
    [TestMethod]
    public void Start_SubtractsPositiveLookbackAndOverlap()
    {
        var now = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual(
            new DateTime(2026, 7, 14, 9, 55, 0, DateTimeKind.Utc),
            SampleFetchWindow.Start(now, TimeSpan.FromHours(2), TimeSpan.FromMinutes(5)));
    }
}
