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

        public async Task<string> PostAsync(string path, HttpContent content)
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
                HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                RvtLogger.Logger.LogError(
                    "Omnidots POST request failed statusCode={StatusCode}",
                    (int)response.StatusCode);
                throw AdapterException.Of("Omnidots API request failed.");
            }

            var reply = await response.Content.ReadAsStringAsync();
            return reply;
        }
    }
}
