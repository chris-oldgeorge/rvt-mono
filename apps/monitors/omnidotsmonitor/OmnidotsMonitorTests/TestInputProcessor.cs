
using Omnidots.Api;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using AlertActivityTimeDto = Rvt.Monitor.Common.Notifications.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Notifications.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Notifications.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Notifications.RvtContactDto;
namespace OmnidotsAdapterTests
{

    [TestClass]
    public class TestInputProcessor
    {

        [TestMethod]
        public void TestCorrectQueryParams_Success()
        {
            var query = "?foo=11223&bar=hello";
            Assert.AreEqual(11223, OmnidotsQueryProcessor.GetIntParameter(query, "foo"));
            Assert.AreEqual("hello", OmnidotsQueryProcessor.GetStringParameter(query, "bar"));
        }

        [TestMethod]
        public void TestNonIntegerParam_ThrowsCorrectException()
        {
            var exception = Assert.ThrowsExactly<AdapterException>(() =>
            {
                _ = OmnidotsQueryProcessor.GetIntParameter("?baz=wx2&bar=98922", "baz");

            });
            Assert.AreEqual("Failed ! baz must be an Integer", exception.Message);
            Assert.IsInstanceOfType(exception.InnerException, typeof(FormatException));
        }

    }
}
