
using Omnidots.Api;
using Rvt.Monitor.Common.Diagnostics;
namespace OmnidotsAdapterTests
{

    [TestClass]
    public class TestInputProcessor
    {

        [TestMethod]
        public void TestCorrectQueryParams_Success()
        {
            string query = "?foo=11223&bar=hello";
            Assert.AreEqual(11223, OmnidotsQueryProcessor.GetIntParameter(query, "foo"));
            Assert.AreEqual("hello", OmnidotsQueryProcessor.GetStringParameter(query, "bar"));
        }

        [TestMethod]
        public void TestNonIntegerParam_ThrowsCorrectException()
        {
            AdapterException exception = Assert.ThrowsExactly<AdapterException>(() =>
            {
                _ = OmnidotsQueryProcessor.GetIntParameter("?baz=wx2&bar=98922", "baz");

            });
            Assert.AreEqual("Failed ! baz must be an Integer", exception.Message);
            Assert.IsInstanceOfType<FormatException>(exception.InnerException);
        }

    }
}
