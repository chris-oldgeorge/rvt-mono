using System.Text;
using System.Text.Json;
using AirQ.Common;
using AirQ.Model.Http;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;

namespace AirQ.Api.Http
{
    // Summary: Vendor HTTP gateway for the AirQ API - request building, calls, and response parsing.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the AirQApi partials (AirQApi, AirQApiMonitors, AirQApiMonitorsNoiseLevels).
    public class AirQHttpGateway
    {
        private readonly IHttpClient httpClient;

        public AirQHttpGateway(IHttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public List<InstrumentResponse> GetMonitors(string userId, string userAuth)
        {
            string response;
            try
            {
                response = DoGetInstrumentList(userId, userAuth).Result;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("GetMonitors", e);
            }

            return ParseResponse<List<InstrumentResponse>>(response);
        }

        public List<MetaDataResponse> GetMetaData(string userId, string userAuth, string serialId)
        {
            RvtLogger.Logger.LogInformation("AirQAdapter GetMetadata userId={Value1}", SensitiveLogRedactor.Redact(userId));
            var response = DoGetMetaData(userId, userAuth, serialId).Result;
            return ParseResponse<List<MetaDataResponse>>(response);
        }

        public List<SampleResponse> HttpGetLatestSamples(string userId, string userAuth, string serialId, ref DateTime latestDateTime)
        {
            string response;
            try
            {
                response = DoGetLatestData(userId, userAuth, serialId).Result;
                RvtLogger.Logger.LogDebug("GetLatestSamples response={Value1}", SensitiveLogRedactor.RedactJson(response));
            }
            catch (Exception e)
            {
                throw AdapterException.Of("GetLatestSamples", e);
            }
            var samples = ParseResponse<List<SampleResponse>>(response);
            return TruncateByLatestMills(samples, ref latestDateTime);
        }

        public List<SampleResponse> GetSamplesForDate(string userId, string userAuth,
                            string serialId, string date)
        {
            string response;
            try
            {
                RvtLogger.Logger.LogDebug("GetSamplesForDate for SerialId={Value1}", serialId);
                response = DoGetDataForDate(userId, userAuth, serialId, date).Result;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("GetSamplesForDate", e);
            }
            return ParseResponse<List<SampleResponse>>(response);
        }

        #region ApiCalls

        private async Task<string> DoGetInstrumentList(string userId, string token)
        {
            var path = BuildQueryPath("/instrumentList", ("userID", userId), ("token", token));
            return await httpClient.GetAsync(path);
        }

        private async Task<string> DoGetMetaData(string userId, string token, string serialId)
        {
            var path = BuildQueryPath("/latestMetaData", ("userID", userId), ("token", token), ("instrumentID", serialId));
            RvtLogger.Logger.LogInformation("Path={Path}", SensitiveLogRedactor.RedactUrl(path));
            return await httpClient.GetAsync(path);
        }

        private async Task<string> DoGetDataForDate(string userId, string token,
                                                    string instrumentId, string date)
        {
            var path = BuildQueryPath(
                "/dataForDate",
                ("userID", userId),
                ("date", date),
                ("token", token),
                ("instrumentID", instrumentId));
            return await httpClient.GetAsync(path);
        }

        private async Task<string> DoGetLatestData(string userId, string token,
                                                    string instrumentId)
        {
            var path = BuildQueryPath("/latestData", ("userID", userId), ("token", token), ("instrumentID", instrumentId));
            return await httpClient.GetAsync(path);
        }

        #endregion // ApiCalls

        private static string BuildQueryPath(string path, params (string Name, string Value)[] parameters) =>
            path + "?" + string.Join("&", parameters.Select(parameter =>
                parameter.Name + "=" + Uri.EscapeDataString(parameter.Value)));

        private static T ParseResponse<T>(string json) where T : new()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new T();
                }

                return JsonSerializer.Deserialize<T>(json)!;
            }
            catch (JsonException e)
            {
                // could be an error response
                var errors = ParseErrorResponse(json);
                if (errors != null && errors.Count > 0)
                {
                    if (errors.Count == 1 && errors[0].Response != null && errors[0].Response!.Contains("No data available!", StringComparison.OrdinalIgnoreCase))
                    {
                        return new T(); // Return empty dataset
                    }
                    var sb = new StringBuilder(errors[0].Response!);
                    for (var i = 1; i < errors.Count; i++)
                    {
                        sb.Append(' ');
                        sb.Append(errors[i].Response);
                    }

                    throw AdapterException.Of(SensitiveLogRedactor.RedactJson(sb.ToString()));
                }
                else
                {
                    throw AdapterException.Of("Failed to parse JSON !", e);
                }
            }
        }

        private static List<ErrorResponse>? ParseErrorResponse(string response)
        {
            try
            {
                return JsonSerializer.Deserialize<List<ErrorResponse>>(response)!;
            }
            catch (JsonException e)
            {
                RvtLogger.Logger.LogWarning(e, "Could not parse error message from response={Value1}", SensitiveLogRedactor.RedactJson(response));
                return null;
            }
        }

        private static List<SampleResponse> TruncateByLatestMills(List<SampleResponse> samples, ref DateTime latestDateTime)
        {
            var removeList = new List<SampleResponse>();
            foreach (var sample in samples)
            {
                var utcDateTime = DateTimeUtil.ToUtc((DateTime)sample.Utc!);

                if (utcDateTime > DateTime.UtcNow)
                {
                    RvtLogger.Logger.LogWarning("TruncateByLatestMills WARNING sample UTC={Value1} timestamp in the future ", sample.Utc);
                }

                if (utcDateTime > latestDateTime)
                {
                    latestDateTime = utcDateTime;
                }
                else
                {
                    removeList.Add(sample);
                }
            }
            foreach (var sample in removeList)
            {
                samples.Remove(sample);
            }
            return samples;
        }
    }
}
