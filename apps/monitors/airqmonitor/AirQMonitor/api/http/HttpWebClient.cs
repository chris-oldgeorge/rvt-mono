using System.Net;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQ.Api.Http
{

    public class HttpWebClient<T> : IHttpClient
    {
        private readonly HttpClient httpClient;

        public HttpWebClient(string baseUrl)
        {
            httpClient = new()
            {
                BaseAddress = new Uri(baseUrl),
            };
            httpClient.DefaultRequestHeaders.Add("accept", "application/json");
        }

        public async Task<string> GetAsync(string path)
        {
            RvtLogger.Logger.LogDebug("HttpWebClient GetAsync path={Value1}", SensitiveLogRedactor.RedactUrl(path));
            var response = await httpClient.GetAsync(path);
            var reply = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw AdapterException.Of("HTTP ERROR response=", SensitiveLogRedactor.RedactJson(reply));
            }
            return reply;
        }

    }
}
