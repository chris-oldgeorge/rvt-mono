// File summary: Provides shared infrastructure utilities used across portal services.
// Major updates:
// - 2026-07-08 pending Removed console prompts and async-without-await warnings from blob utility cleanup.
// - 2026-06-26 Removed remaining Sonar-reported dead locals and encapsulated blob service fields.
// - 2026-06-25 Resolved nullable-reference warnings on BlobStorage settings.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Azure;
using Azure.Storage.Blobs.Models;
using System.Text;

namespace RVT.Utilities
{
    public class AzureBlobService
    {
        public enum Container
        {
            ArchiveContainer,
            ReportContainer,
            ReportFolder,
            AudioFolder
        }

        private readonly BlobServiceClient blobServiceClient;
        private readonly BlobContainerClient monitorContainerClient;
        private readonly BlobStorage config;

        // Function summary: Initializes this type with the dependencies required by its workflow.
        public AzureBlobService()
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().SetBasePath(Environment.CurrentDirectory).AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configurationRoot = configurationBuilder.Build();

            config = new BlobStorage();
            configurationRoot.GetSection("BlobStorage").Bind(config);

            blobServiceClient = CreateBlobServiceClient(config.blobConnectionString, config.blobServiceUri);

            // Create a container client
            var containerName = "monitor-pictures";
            monitorContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }

        // Function summary: Initializes this type with the dependencies required by its workflow.
        public AzureBlobService(Container container)
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().SetBasePath(Environment.CurrentDirectory).AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configurationRoot = configurationBuilder.Build();

            config = new BlobStorage();
            configurationRoot.GetSection("BlobStorage").Bind(config);

            string containerFolder = string.Empty;
            switch (container)
            {
                case Container.ArchiveContainer:
                    containerFolder = config.ArchiveContainer;
                    break;
                case Container.ReportContainer:
                    containerFolder = config.ReportContainer;
                    break;
                case Container.AudioFolder:
                    containerFolder = config.AudioFolder;
                    break;
            }



            blobServiceClient = CreateBlobServiceClient(config.blobConnectionString, config.blobServiceUri);

            // Create a container client
            var containerName = containerFolder;
            monitorContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }

        // Function summary: Handles the upload stream workflow for this module.
        public async Task<string> UploadStream(Stream FileStream, string blobName)
        {
            var blobClient = monitorContainerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(FileStream, true);
            FileStream.Close();

            var blob = monitorContainerClient.GetBlockBlobClient(blobName);
            return blob.Name;
        }

        // Function summary: Removes delete data for the current workflow.
        public async Task Delete(string blobName)
        {
            var blobClient = monitorContainerClient.GetBlobClient(blobName);

            await blobClient.DeleteAsync();
        }

        // Function summary: Returns a temporary public URI for a monitor blob.
        public Task<string?> GetPubliciUri(string blobName)
        {
            var blobClient = monitorContainerClient.GetBlobClient(blobName);
            if (!blobClient.CanGenerateSasUri)
            {
                return Task.FromResult<string?>(blobClient.Uri.AbsoluteUri);
            }

            var sasUri = blobClient.GenerateSasUri(Azure.Storage.Sas.BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
            var signedUrl = blobClient.Uri + sasUri.Query;
            return Task.FromResult<string?>(signedUrl);
        }

        // Function summary: Returns a temporary public URI for an archive blob.
        public Task<string?> GetPubliciArchiveUri(string blobName)
        {
            // Create a container client
            var archiveContainerClient = blobServiceClient.GetBlobContainerClient(config.ArchiveContainer);

            var blobClient = archiveContainerClient.GetBlobClient(blobName);
            if (!blobClient.CanGenerateSasUri)
            {
                return Task.FromResult<string?>(blobClient.Uri.AbsoluteUri);
            }

            var sasUri = blobClient.GenerateSasUri(Azure.Storage.Sas.BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));
            var signedUrl = blobClient.Uri + sasUri.Query;
            return Task.FromResult<string?>(signedUrl);
        }

        // Function summary: Handles the upload test workflow for this module.
        public Uri? UploadTest(string filePath)
        {
            Uri? uri = null;

            if (!monitorContainerClient.Exists())
            {
                // Create the container if it doesn't exist
                monitorContainerClient.Create();
            }

            string fileContents = "Message";

            using (MemoryStream ms = new MemoryStream())
            {
                var sw = new StreamWriter(ms, new UnicodeEncoding());
                try
                {
                    sw.Write(fileContents);
                    sw.Flush();//otherwise you are risking empty stream
                    ms.Seek(0, SeekOrigin.Begin);

                    var blobClient = monitorContainerClient.GetBlobClient(filePath);
                    blobClient.Upload(ms);
                    var blob = monitorContainerClient.GetBlockBlobClient(filePath);

                    uri = blob.Uri;
                }
                finally
                {
                    sw.Dispose();
                }
            }

            return uri;
        }
        // Function summary: Ensures the configured blob containers exist.
        public void CreateBlobs()
        {
            CreateIfNotExists(config.ReportContainer);
            CreateIfNotExists(config.ArchiveContainer);
            CreateIfNotExists(config.MonitorImagesContainer);
        }
        // Function summary: Creates a blob container when it is missing.
        private void CreateIfNotExists(string containerName)
        {
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            if (!containerClient.Exists())
            {
                // Create the container if it doesn't exist
                containerClient.Create();
            }

        }

        // Function summary: Returns the blob containers visible to the configured storage client.
        public List<string> ListContainers()
        {
            var containers = new List<string>();
            try
            {
                // Call the listing operation and enumerate the result segment.
                var resultSegment = blobServiceClient.GetBlobContainers(BlobContainerTraits.Metadata, "", default).AsPages(default, null);

                foreach (Azure.Page<BlobContainerItem> containerPage in resultSegment)
                {
                    foreach (BlobContainerItem containerItem in containerPage.Values)
                    {
                        containers.Add(containerItem.Name);
                    }
                }
            }
            catch (RequestFailedException)
            {
                throw;
            }
            return containers;
        }

        public class BlobStorage
        {
            public string blobConnectionString { get; set; } = null!;
            public string blobServiceUri { get; set; } = null!;
            public string MonitorImagesContainer { get; set; } = null!;
            public string ArchiveContainer { get; set; } = null!;
            public string ReportContainer { get; set; } = null!;
            public string ReportFolder { get; set; } = null!;
            public string AudioFolder { get; set; } = null!;
        }

        // Function summary: Creates blob service client data for the current workflow.
        private static BlobServiceClient CreateBlobServiceClient(string connectionString, string serviceUri)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return new BlobServiceClient(connectionString);
            }

            if (!string.IsNullOrWhiteSpace(serviceUri))
            {
                return new BlobServiceClient(new Uri(serviceUri), new DefaultAzureCredential());
            }

            throw new InvalidOperationException("Blob storage settings are missing. Configure BlobStorage:blobServiceUri for managed identity or BlobStorage:blobConnectionString as a temporary fallback.");
        }
    }
}
