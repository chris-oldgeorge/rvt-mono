using System.Text.Json;
using AirQ.Model.Dto;
using AirQ.Model.Http;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

using AlertActivityTimeDto = Rvt.Monitor.Common.Rules.AlertActivityTimeDto;
using ContactMethod = Rvt.Monitor.Common.Rules.ContactMethod;
using NotificationDto = Rvt.Monitor.Common.Rules.NotificationDto;
using RvtContactDto = Rvt.Monitor.Common.Rules.RvtContactDto;
namespace AirQMonitorTests
{

    [TestClass]
    public class TestDto
    {

        [TestMethod]
        public void TestSampleResponse_ToNoiseDto_Success()
        {

            var json = TestUtil.ReadTextFromFile("testdata/latest_samples.json");

            var samples = JsonSerializer.Deserialize<List<SampleResponse>>(json)!;

            Assert.IsNotNull(samples);
            Assert.AreEqual(1, samples.Count);

            var noiseDto = new NoiseDto(samples[0]);
            Assert.IsNotNull(samples);
            Assert.AreEqual(DateTime.Parse("2023-09-18T11:30:00"), noiseDto.SampleTime);

            Assert.AreEqual(44.75, noiseDto.LAeq);
            Assert.AreEqual(61.28, noiseDto.LAmax);
            Assert.AreEqual(43.00, noiseDto.LA90);
            Assert.AreEqual(44.47, noiseDto.LA10);
            Assert.AreEqual(54.19, noiseDto.LCeq);
            Assert.AreEqual(82.81, noiseDto.LCmax);
            Assert.AreEqual(47.56, noiseDto.LC90);
            Assert.AreEqual(51.22, noiseDto.LC10);
        }

        [TestMethod]
        public void TestSampleResponse_ToNoiseDto_ThrowsCorrectException()
        {

            var json = TestUtil.ReadTextFromFile("testdata/latest_samples.json");

            var samples = JsonSerializer.Deserialize<List<SampleResponse>>(json)!;

            samples![0].Data![0].Value = "123.abc";

            var exception = Assert.ThrowsExactly<AdapterException>(() =>
            {
                _ = new NoiseDto(samples[0]);
            });

            Assert.AreEqual("Failed ! LAeq(T) was not a number", exception.Message);
            Assert.IsInstanceOfType(exception.InnerException, typeof(FormatException));
            Assert.AreEqual("The input string '123.abc' was not in a correct format.", exception.InnerException!.Message);

        }

    }
}
