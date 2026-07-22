using System.Net;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace Svantek.Api.Http
{

    public class HttpWebClient<T> : IHttpClient
    {
        private readonly HttpClient httpClient;

        public HttpWebClient(string baseUrl)
            : this(baseUrl, new HttpClient())
        {
        }

        public HttpWebClient(string baseUrl, HttpClient httpClient)
        {
            this.httpClient = httpClient;
            this.httpClient.BaseAddress ??= new Uri(baseUrl);
            if (!this.httpClient.DefaultRequestHeaders.Contains("accept"))
            {
                this.httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            }
        }

        public async Task<string> GetAsync(string path, CancellationToken cancellationToken = default)
        {
            RvtLogger.Logger.LogDebug("HttpWebClient GetAsync path={Value1}", SensitiveLogRedactor.RedactUrl(path));
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var reply = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
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
            RvtLogger.Logger.LogDebug("HttpWebClient PostAsync path={Value1}", SensitiveLogRedactor.RedactUrl(path));

            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Content = content;

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var reply = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                RvtLogger.Logger.LogError("Config request failed with error={Value1}", SensitiveLogRedactor.RedactJson(reply));
                throw AdapterException.Of("HTTP ERROR response=", SensitiveLogRedactor.RedactJson(reply));
            }
            return reply;
        }

        public async Task<byte[]> GetByteArrayAsync(
            string path,
            MultipartFormDataContent content,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Content = content;

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var reply = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                RvtLogger.Logger.LogError("File request failed with error={Value1}", response.StatusCode);
                throw AdapterException.Of("HTTP ERROR response=" + response.StatusCode);
            }
            return reply;
        }
    }
}
