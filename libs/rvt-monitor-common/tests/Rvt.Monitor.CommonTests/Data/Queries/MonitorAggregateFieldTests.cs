using Rvt.Monitor.Common.Data.Queries;

namespace Rvt.Monitor.CommonTests.Data.Queries;

[TestClass]
public sealed class MonitorAggregateFieldTests
{
    [TestMethod]
    public void CreateAverageField_RejectsUnsupportedField()
    {
        Assert.ThrowsExactly<NotSupportedException>(() =>
            MonitorAggregateField<object>.Average("bad field", row => 0));
    }

    [TestMethod]
    public void CreateAverageField_AcceptsKnownSafeName()
    {
        var field = MonitorAggregateField<object>.Average("Pm10", row => 10);

        Assert.AreEqual("Pm10", field.Name);
        Assert.IsFalse(field.UseMaximum);
    }

    [TestMethod]
    public void CreateMaximumField_AcceptsKnownSafeName()
    {
        var field = MonitorAggregateField<object>.Maximum("LAmax", row => 10);

        Assert.AreEqual("LAmax", field.Name);
        Assert.IsTrue(field.UseMaximum);
    }
}
