using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api.Http
{

    public class HttpWebClient : IHttpClient
    {

        /// <summary>
        /// Bounds every vendor call. Without an explicit value the 100 second
        /// default applies, so an unresponsive endpoint stalled the whole
        /// import for that long on each request.
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
            this.httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            this.httpClient.Timeout = RequestTimeout;
        }

        public async Task<string> GetAsync(string path, CancellationToken cancellationToken = default)
        {
            RvtLogger.Logger.LogDebug("HttpWebClient GetAsync path={Value1}", SensitiveLogRedactor.RedactUrl(path));
            using var response = await httpClient.GetAsync(path, cancellationToken);
            string reply = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw AdapterException.Of("HTTP ERROR response=", SensitiveLogRedactor.RedactJson(reply));
            }

            return reply;
        }

        public async Task<string> PostAsync(string path, HttpContent content, CancellationToken cancellationToken = default)
        {
            RvtLogger.Logger.LogDebug("HttpWebClient PostAsync path={Value1}", SensitiveLogRedactor.RedactUrl(path));

            if (RvtConfig.USE_TOKEN && path.StartsWith("/api/v1/user/authenticate"))
            {
                var resp = new TokenResponse();
                resp.Ok = true;
                resp.Token = RvtConfig.TOKEN;
                return JsonSerializer.Serialize(resp);
            }

            using var request = new HttpRequestMessage(new HttpMethod("POST"), path);
            request.Content = content;

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                RvtLogger.Logger.LogError(
                    "Omnidots POST request failed statusCode={StatusCode}",
                    (int)response.StatusCode);
                throw AdapterException.Of("Omnidots API request failed.");
            }

            string reply = await response.Content.ReadAsStringAsync(cancellationToken);
            return reply;
        }
    }
}
