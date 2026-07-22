using Rvt.Monitor.Common.Utilities;

namespace Rvt.Monitor.CommonTests.Utilities;

[TestClass]
public sealed class DateTimeUtilTests
{
    [TestMethod]
    public void AsUtc_UtcValue_ReturnsValueUnchanged()
    {
        var value = new DateTime(2026, 7, 16, 10, 30, 0, DateTimeKind.Utc);

        var result = DateTimeUtil.AsUtc(value);

        Assert.AreEqual(value, result);
        Assert.AreEqual(value.Ticks, result.Ticks);
        Assert.AreEqual(DateTimeKind.Utc, result.Kind);
    }

    [TestMethod]
    public void AsUtc_LocalValue_ConvertsTheRepresentedInstant()
    {
        var value = new DateTime(2026, 7, 16, 10, 30, 0, DateTimeKind.Local);

        var result = DateTimeUtil.AsUtc(value);

        Assert.AreEqual(value.ToUniversalTime(), result);
        Assert.AreEqual(DateTimeKind.Utc, result.Kind);
    }

    [TestMethod]
    public void AsUtc_UnspecifiedValue_PreservesTicksAndMarksUtc()
    {
        var value = new DateTime(2026, 7, 16, 10, 30, 0, DateTimeKind.Unspecified);

        var result = DateTimeUtil.AsUtc(value);

        Assert.AreEqual(value.Ticks, result.Ticks);
        Assert.AreEqual(DateTimeKind.Utc, result.Kind);
    }

    [TestMethod]
    public void AsUtc_NullableNull_PreservesNull()
    {
        DateTime? value = null;

        var result = DateTimeUtil.AsUtc(value);

        Assert.IsNull(result);
    }
}
