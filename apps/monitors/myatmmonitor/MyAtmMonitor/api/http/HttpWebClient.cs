using MyAtm.Model.Config;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Http;

namespace MyAtm.Api.Http
{

    public class HttpWebClient : IHttpClient
    {

        private readonly VendorHttpTransport _transport;

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
            if (maxResponseBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxResponseBytes));
            }

            httpClient.BaseAddress = new Uri(baseUrl);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Api-Key", token);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("accept", "application/json");
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            _transport = new VendorHttpTransport(httpClient, requestPolicy, maxResponseBytes);
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
            using VendorHttpResponse response = await _transport.SendAsync(HttpMethod.Get, path, null, cancellationToken);
            if (!response.IsOk)
            {
                // Deliberately never reads the vendor body on failure.
                throw AdapterException.Of($"HTTP ERROR status={(int)response.StatusCode} path={path}");
            }

            return await response.ReadStringAsync(cancellationToken);
        }

    }
}
