namespace SvantekMonitorTests;

[TestClass]
public sealed class PostgreSqlFixtureDateTimeTests
{
    [TestMethod]
    public void ParseUtc_ReturnsUtcDateTime()
    {
        var timestamp = PostgreSqlFixtureDateTime.ParseUtc("2023-10-18T14:35:42Z");

        Assert.AreEqual(DateTimeKind.Utc, timestamp.Kind);
    }
}
