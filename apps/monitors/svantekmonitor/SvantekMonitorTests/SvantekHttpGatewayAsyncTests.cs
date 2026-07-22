using Moq;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Api.Http;
using Svantek.Model.Http;

namespace SvantekMonitorTests;

[TestClass]
public sealed class SvantekHttpGatewayAsyncTests
{
    private const string ProjectsJson = """
        {"status":"ok","projects":[{"id":"7","project_name":"Project 7"}]}
        """;

    private const string ProjectFilesJson = """
        {"status":"ok","files":[["20260713_09_59_30.WAV",3,"20260713",2048,"SV307","station-123","2026-07-13 10:00:00",0,1]],"files_size":1}
        """;

    private const string StationsJson = """
        {"status":"ok","stations":[{"serial":12345,"type":"SV307"}]}
        """;

    private const string MultiDataJson = """
        {"status":"ok","data":[{"point":3,"data":{"status":"ok","results":[]}}]}
        """;

    [TestMethod]
    public async Task AsyncOperations_PassTheExactCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var http = new Mock<IHttpClient>(MockBehavior.Strict);
        http.SetupSequence(client => client.PostAsync(
                "projects-get-data.php",
                It.IsAny<HttpContent>(),
                token))
            .ReturnsAsync(ProjectsJson)
            .ReturnsAsync(ProjectFilesJson);
        http.Setup(client => client.PostAsync(
                "stations-get-list.php",
                It.IsAny<HttpContent>(),
                token))
            .ReturnsAsync(StationsJson);
        http.Setup(client => client.PostAsync(
                "projects-get-result-data-multi-point.php",
                It.IsAny<HttpContent>(),
                token))
            .ReturnsAsync(MultiDataJson);
        http.Setup(client => client.GetByteArrayAsync(
                "projects-get-data.php",
                It.IsAny<MultipartFormDataContent>(),
                token))
            .ReturnsAsync([82, 73, 70, 70]);
        var gateway = new SvantekHttpGateway(http.Object, "test-api-key");

        var projects = await gateway.GetProjectsAsync(token);
        var files = await gateway.GetProjectFilesAsync("7", "3", "20260713", cancellationToken: token);
        var stations = await gateway.GetStationsAsync(token);
        var data = await gateway.GetDataMultiAsync("7", [new MultiDataArgument { point = 3 }], token);
        var sound = await gateway.GetSoundFileAsync(7, 3, "SV307", "20260713", "12345", "sound.wav", token);

        Assert.HasCount(1, projects);
        Assert.HasCount(1, files);
        Assert.HasCount(1, stations);
        Assert.HasCount(1, data);
        CollectionAssert.AreEqual(new byte[] { 82, 73, 70, 70 }, sound);
        http.VerifyAll();
    }

    [TestMethod]
    public async Task GetStationsAsync_AwaitsTheAdapterResponse()
    {
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var http = new Mock<IHttpClient>();
        http.Setup(client => client.PostAsync(
                "stations-get-list.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .Returns(response.Task);
        var gateway = new SvantekHttpGateway(http.Object, "test-api-key");

        var stationsTask = gateway.GetStationsAsync();

        Assert.IsFalse(stationsTask.IsCompleted);
        response.SetResult(StationsJson);
        var stations = await stationsTask;
        Assert.HasCount(1, stations);
    }

    [TestMethod]
    public async Task GetStationsAsync_WrapsNonCancellationAdapterFailure()
    {
        var adapterFailure = new IOException("vendor unavailable");
        var http = new Mock<IHttpClient>();
        http.Setup(client => client.PostAsync(
                "stations-get-list.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ThrowsAsync(adapterFailure);
        var gateway = new SvantekHttpGateway(http.Object, "test-api-key");

        var exception = await Assert.ThrowsExactlyAsync<AdapterException>(() => gateway.GetStationsAsync());

        Assert.AreEqual("GetStations", exception.Message);
        Assert.AreSame(adapterFailure, exception.InnerException);
    }

    [TestMethod]
    public async Task GetStationsAsync_PreservesCallerCancellationException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var expected = new OperationCanceledException(cancellation.Token);
        var http = new Mock<IHttpClient>();
        http.Setup(client => client.PostAsync(
                "stations-get-list.php",
                It.IsAny<HttpContent>(),
                cancellation.Token))
            .ThrowsAsync(expected);
        var gateway = new SvantekHttpGateway(http.Object, "test-api-key");

        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => gateway.GetStationsAsync(cancellation.Token));

        Assert.AreSame(expected, exception);
    }
}
