using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Http;

namespace Svantek.Api.Http
{

    public class HttpWebClient : IHttpClient
    {
        private const int _maximumJsonResponseBytes = 4 * 1024 * 1024;
        private const int _maximumRecordingBytes = 64 * 1024 * 1024;

        /// <summary>
        /// Bounds every vendor call. Without an explicit value the 100 second
        /// default applies, so an unresponsive endpoint stalled the whole
        /// import for that long on each request.
        /// </summary>
        internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private readonly VendorHttpTransport _downloadTransport;
        private readonly VendorHttpTransport _transport;

        public HttpWebClient(string baseUrl)
            : this(baseUrl, new HttpClient())
        {
        }

        public HttpWebClient(string baseUrl, HttpClient httpClient)
        {
            httpClient.BaseAddress ??= new Uri(baseUrl);
            if (!httpClient.DefaultRequestHeaders.Contains("accept"))
            {
                httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            }
            httpClient.Timeout = RequestTimeout;
            _transport = new VendorHttpTransport(
                httpClient,
                maxResponseBytes: _maximumJsonResponseBytes);
            _downloadTransport = new VendorHttpTransport(
                httpClient,
                maxResponseBytes: _maximumRecordingBytes);
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

        public async Task<string> PostAsync(
            string path,
            HttpContent content,
            CancellationToken cancellationToken = default)
        {
            if (RvtLogger.Logger.IsEnabled(LogLevel.Debug))
            {
                RvtLogger.Logger.LogDebug("HttpWebClient PostAsync path={Value1}", SensitiveLogRedactor.RedactUrl(path));
            }

            using VendorHttpResponse response = await _transport.SendAsync(HttpMethod.Post, path, content, cancellationToken);
            string reply = await response.ReadStringAsync(cancellationToken);
            if (!response.IsOk)
            {
                RvtLogger.Logger.LogError("Config request failed with error={Value1}", SensitiveLogRedactor.RedactJson(reply));
                throw AdapterException.Of("HTTP ERROR response=", SensitiveLogRedactor.RedactJson(reply));
            }
            return reply;
        }

        /// <summary>
        /// Sends the multipart request as a POST and returns the raw bytes.
        /// Previously misnamed <c>GetByteArrayAsync</c> despite always posting.
        /// </summary>
        public async Task<byte[]> PostForBytesAsync(
            string path,
            MultipartFormDataContent content,
            CancellationToken cancellationToken = default)
        {
            using VendorHttpResponse response = await _downloadTransport.SendAsync(
                HttpMethod.Post,
                path,
                content,
                cancellationToken);
            if (!response.IsOk)
            {
                RvtLogger.Logger.LogError("File request failed with error={Value1}", response.StatusCode);
                throw AdapterException.Of("HTTP ERROR response=" + response.StatusCode);
            }
            return await response.ReadBytesAsync(cancellationToken);
        }
    }
}
