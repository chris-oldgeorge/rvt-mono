using Rvt.Storage.Local;

namespace Rvt.Storage.Tests.Contracts;

[TestClass]
public sealed class LocalObjectStorageContractTests : ObjectStorageClientContractTests
{
    protected override Task<IObjectStorageClientFixture> CreateFixtureAsync() =>
        Task.FromResult<IObjectStorageClientFixture>(new LocalFixture());

    private sealed class LocalFixture : IObjectStorageClientFixture
    {
        private readonly string rootPath = Path.Combine(
            Path.GetTempPath(),
            $"rvt-storage-contract-{Guid.NewGuid():N}");

        public LocalFixture()
        {
            Client = new LocalObjectStorageClient(
                "contract-recordings",
                new LocalStorageOptions
                {
                    RootPath = rootPath,
                    Container = "contract-tests",
                    Prefix = "fixture-root",
                });
        }

        public IObjectStorageClient Client { get; }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
