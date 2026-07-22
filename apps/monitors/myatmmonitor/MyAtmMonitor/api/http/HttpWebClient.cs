using System.Net;
using System.Text;
using MyAtm.Model.Config;
using Rvt.Monitor.Common.Diagnostics;

namespace MyAtm.Api.Http
{

    public class HttpWebClient<T> : IHttpClient
    {

        private readonly HttpClient httpClient;
        private readonly MyAtmRequestPolicy requestPolicy;
        private readonly int maxResponseBytes;


        public HttpWebClient(string baseUrl, string token)
            : this(baseUrl, token, new HttpClient(), new MyAtmRequestPolicy(), 4 * 1024 * 1024)
        {
        }

        public HttpWebClient(
            string baseUrl,
            string token,
            HttpClient httpClient,
            MyAtmRequestPolicy requestPolicy,
            int maxResponseBytes = 4 * 1024 * 1024)
        {
            this.httpClient = httpClient;
            this.requestPolicy = requestPolicy;
            this.maxResponseBytes = maxResponseBytes > 0
                ? maxResponseBytes
                : throw new ArgumentOutOfRangeException(nameof(maxResponseBytes));
            this.httpClient.BaseAddress = new Uri(baseUrl);
            this.httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Api-Key", token);
            this.httpClient.DefaultRequestHeaders.TryAddWithoutValidation("accept", "application/json");
            this.httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public HttpWebClient(
            MyAtmVendorOptions options,
            HttpClient httpClient,
            MyAtmRequestPolicy requestPolicy)
            : this(
                options.BaseUrl,
                options.ApiKey,
                httpClient,
                requestPolicy,
                options.MaxResponseBytes)
        {
        }

        public async Task<string> GetAsync(string path, CancellationToken cancellationToken = default)
        {
            /*
             Also, you can customize responses for the measurement endpoints using the OData query language. for example, include only required fields by specifying $select and $expand query parameters:
            $select=timestamp
            $expand=pm1,pm2_5,pm10,weather_t,weather_p,weather_rh

            Also, you can filter the results. For example, include measurements since the 10th of October (UTC midnight)
            $filter=timestamp gt 2023-10-10T00:00:00Z

            And include more than 50 results (the default response page size). Let's put the maximum supported page size:
            $top=50000
            */
            for (var attempt = 1; ; attempt++)
            {
                await requestPolicy.WaitForPermitAsync(cancellationToken);
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return await ReadBoundedContentAsync(response.Content, cancellationToken);
                }

                if (requestPolicy.ShouldRetry(response.StatusCode, attempt))
                {
                    await requestPolicy.DelayAsync(requestPolicy.GetRetryDelay(response, attempt), cancellationToken);
                    continue;
                }

                throw AdapterException.Of($"HTTP ERROR status={(int)response.StatusCode} path={path}");
            }
        }

        private async Task<string> ReadBoundedContentAsync(
            HttpContent content,
            CancellationToken cancellationToken)
        {
            var contentLength = content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > maxResponseBytes)
            {
                throw AdapterException.Of($"HTTP response exceeded the configured {maxResponseBytes}-byte limit.");
            }

            await using var source = await content.ReadAsStreamAsync(cancellationToken);
            using var destination = new MemoryStream();
            var buffer = new byte[Math.Min(81920, maxResponseBytes)];
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (destination.Length + read > maxResponseBytes)
                {
                    throw AdapterException.Of($"HTTP response exceeded the configured {maxResponseBytes}-byte limit.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            return Encoding.UTF8.GetString(destination.GetBuffer(), 0, checked((int)destination.Length));
        }

    }
}
