using System.Net;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQ.Api.Http
{

    public class HttpWebClient<T> : IHttpClient
    {
        /// <summary>
        /// Bounds every vendor call. Without an explicit value the 100 second
        /// default applies, so an unresponsive endpoint stalled the whole
        /// fleet import for that long on each request.
        /// </summary>
        internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private readonly HttpClient httpClient;

        public HttpWebClient(string baseUrl)
            : this(baseUrl, new HttpClient())
        {
        }

        internal HttpWebClient(string baseUrl, HttpClient httpClient)
        {
            this.httpClient = httpClient;
            this.httpClient.BaseAddress = new Uri(baseUrl);
            this.httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            this.httpClient.Timeout = RequestTimeout;
        }

        public async Task<string> GetAsync(string path, CancellationToken cancellationToken = default)
        {
            RvtLogger.Logger.LogDebug("HttpWebClient GetAsync path={Value1}", SensitiveLogRedactor.RedactUrl(path));
            using HttpResponseMessage response = await httpClient.GetAsync(path, cancellationToken);
            string reply = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw AdapterException.Of("HTTP ERROR response=", SensitiveLogRedactor.RedactJson(reply));
            }
            return reply;
        }

    }
}
