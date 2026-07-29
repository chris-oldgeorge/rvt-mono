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
        using CancellationTokenSource cancellation = new();
        CancellationToken token = cancellation.Token;
        Mock<IHttpClient> http = new(MockBehavior.Strict);
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
        http.Setup(client => client.PostForBytesAsync(
                "projects-get-data.php",
                It.IsAny<MultipartFormDataContent>(),
                token))
            .ReturnsAsync([82, 73, 70, 70]);
        SvantekHttpGateway gateway = new(http.Object, "test-api-key");

        List<Project> projects = await gateway.GetProjectsAsync(token);
        List<ProjectFile> files = await gateway.GetProjectFilesAsync("7", "3", "20260713", cancellationToken: token);
        List<Station> stations = await gateway.GetStationsAsync(token);
        List<MultiData> data = await gateway.GetDataMultiAsync("7", [new MultiDataArgument { point = 3 }], token);
        byte[] sound = await gateway.GetSoundFileAsync(7, 3, "SV307", "20260713", "12345", "sound.wav", token);

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
        TaskCompletionSource<string> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IHttpClient> http = new();
        http.Setup(client => client.PostAsync(
                "stations-get-list.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .Returns(response.Task);
        SvantekHttpGateway gateway = new(http.Object, "test-api-key");

        Task<List<Station>> stationsTask = gateway.GetStationsAsync();

        Assert.IsFalse(stationsTask.IsCompleted);
        response.SetResult(StationsJson);
        List<Station> stations = await stationsTask;
        Assert.HasCount(1, stations);
    }

    [TestMethod]
    public async Task GetStationsAsync_WrapsNonCancellationAdapterFailure()
    {
        IOException adapterFailure = new("vendor unavailable");
        Mock<IHttpClient> http = new();
        http.Setup(client => client.PostAsync(
                "stations-get-list.php",
                It.IsAny<HttpContent>(),
                CancellationToken.None))
            .ThrowsAsync(adapterFailure);
        SvantekHttpGateway gateway = new(http.Object, "test-api-key");

        AdapterException exception = await Assert.ThrowsExactlyAsync<AdapterException>(() => gateway.GetStationsAsync());

        Assert.AreEqual("GetStations", exception.Message);
        Assert.AreSame(adapterFailure, exception.InnerException);
    }

    [TestMethod]
    public async Task GetStationsAsync_PreservesCallerCancellationException()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        OperationCanceledException expected = new(cancellation.Token);
        Mock<IHttpClient> http = new();
        http.Setup(client => client.PostAsync(
                "stations-get-list.php",
                It.IsAny<HttpContent>(),
                cancellation.Token))
            .ThrowsAsync(expected);
        SvantekHttpGateway gateway = new(http.Object, "test-api-key");

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => gateway.GetStationsAsync(cancellation.Token));

        Assert.AreSame(expected, exception);
    }
}
