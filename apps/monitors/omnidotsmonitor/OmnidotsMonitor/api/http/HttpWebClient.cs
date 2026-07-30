using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Http;

namespace Omnidots.Api.Http
{

    public class HttpWebClient : IHttpClient
    {
        private const int _maximumResponseBytes = 4 * 1024 * 1024;

        /// <summary>
        /// Bounds every vendor call. Without an explicit value the 100 second
        /// default applies, so an unresponsive endpoint stalled the whole
        /// import for that long on each request.
        /// </summary>
        internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private readonly VendorHttpTransport _transport;

        public HttpWebClient(string baseUrl)
            : this(baseUrl, new HttpClient())
        {
        }

        internal HttpWebClient(string baseUrl, HttpClient httpClient)
        {
            httpClient.BaseAddress = new Uri(baseUrl);
            httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            httpClient.Timeout = RequestTimeout;
            _transport = new VendorHttpTransport(
                httpClient,
                maxResponseBytes: _maximumResponseBytes);
        }

        public async Task<string> GetAsync(string path, CancellationToken cancellationToken = default)
        {
            if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
            {
                RvtLogger.Logger.LogDebug("HttpWebClient GetAsync path={Value1}", SensitiveLogRedactor.RedactUrl(path));
            }

            using VendorHttpResponse response = await _transport.SendAsync(HttpMethod.Get, path, null, cancellationToken);
            string reply = await response.ReadStringAsync(cancellationToken);
            if (!response.IsOk)
            {
                throw AdapterException.Of("HTTP ERROR response=", SensitiveLogRedactor.RedactJson(reply));
            }

            return reply;
        }

        public async Task<string> PostAsync(string path, HttpContent content, CancellationToken cancellationToken = default)
        {
            if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
            {
                RvtLogger.Logger.LogDebug("HttpWebClient PostAsync path={Value1}", SensitiveLogRedactor.RedactUrl(path));
            }

            using VendorHttpResponse response = await _transport.SendAsync(HttpMethod.Post, path, content, cancellationToken);
            if (!response.IsOk)
            {
                // Deliberately never reads the vendor body on failure: it may
                // carry credentials or tokens, and nothing here needs it.
                RvtLogger.Logger.LogError(
                    "Omnidots POST request failed statusCode={StatusCode}",
                    (int)response.StatusCode);
                throw AdapterException.Of("Omnidots API request failed.");
            }

            return await response.ReadStringAsync(cancellationToken);
        }
    }
}
