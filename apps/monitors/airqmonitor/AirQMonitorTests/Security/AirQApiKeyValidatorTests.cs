using AirQ.Api.Security;
using Microsoft.Extensions.Primitives;

[TestClass]
public sealed class AirQApiKeyValidatorTests
{
    [TestMethod]
    public void Create_RejectsMissingConfiguredKey() =>
        Assert.Throws<InvalidOperationException>(() => AirQApiKeyValidator.Create(null));

    [TestMethod]
    public void IsAuthorized_AcceptsExactlyOneMatchingHeaderValue()
    {
        var validator = AirQApiKeyValidator.Create("monitor-api-key");

        Assert.IsTrue(validator.IsAuthorized(new StringValues("monitor-api-key")));
        Assert.IsFalse(validator.IsAuthorized(StringValues.Empty));
        Assert.IsFalse(validator.IsAuthorized(new StringValues("wrong-key")));
        Assert.IsFalse(validator.IsAuthorized(new StringValues(["monitor-api-key", "wrong-key"])));
    }
}
