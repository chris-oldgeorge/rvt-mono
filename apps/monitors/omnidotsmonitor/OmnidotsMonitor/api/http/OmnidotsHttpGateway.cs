using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Api.Http
{
    // Summary: Vendor HTTP gateway for the Omnidots Honeycomb API - authentication, calls, and response parsing.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApi, OmnidotsApiMonitors, OmnidotsApiVibrationLevels, OmnidotsApiTraces, OmnidotsApiConfiguration).
    public class OmnidotsHttpGateway
    {
        private readonly IHttpClient httpClient;
        private readonly string userId;
        private readonly string userAuth;

        public OmnidotsHttpGateway(IHttpClient httpClient, string userId, string userAuth)
        {
            this.httpClient = httpClient;
            this.userId = userId;
            this.userAuth = userAuth;
        }

        public TokenResponse Authenticate()
        {
            using var content = new MultipartFormDataContent();
            var values = new[]
            {
                new KeyValuePair<string, string>("username", userId),
                new KeyValuePair<string, string>("password", userAuth)
            };

            foreach (var keyValuePair in values)
            {
                content.Add(new StringContent(keyValuePair.Value),
                    String.Format("\"{0}\"", keyValuePair.Key));
            }

            var response = DoAuthenticate(content).Result;
            RvtLogger.Logger.LogDebug("Authenticate response={Value1}", SensitiveLogRedactor.RedactJson(response));
            return ParseJson<TokenResponse>(response);
        }

        public async Task<TokenResponse> AuthenticateAsync(CancellationToken cancellationToken)
        {
            using var content = new MultipartFormDataContent();
            var values = new[]
            {
                new KeyValuePair<string, string>("username", userId),
                new KeyValuePair<string, string>("password", userAuth)
            };

            foreach (var keyValuePair in values)
            {
                content.Add(new StringContent(keyValuePair.Value),
                    String.Format("\"{0}\"", keyValuePair.Key));
            }

            var response = await DoAuthenticate(content).WaitAsync(cancellationToken);
            return ParseJson<TokenResponse>(response);
        }

        public MeasuringPointsResponse ListMeasuringPoints()
        {
            var response = DoListMeasuringPoints(Authenticate().Token!).Result;
            return ParseJson<MeasuringPointsResponse>(response);
        }

        public PeakRecords GetPeakRecords(string token, DateTime startTime, DateTime? endTime, string measuringPointId)
        {
            var response = DoGet(path: "/api/v1/get_peak_records", token: token,
                                     startTime: startTime, endTime: endTime, measuringPointId: measuringPointId).Result;
            return ParseJson<PeakRecords>(response);
        }

        public VeffRecords GetVeffRecords(string token, DateTime startTime, DateTime? endTime, string measuringPointId)
        {
            var response = DoGet("/api/v1/get_veff_records", token,
                 startTime, endTime, measuringPointId).Result;
            return ParseJson<VeffRecords>(response);
        }

        public VdvRecords GetVdvRecords(string token, DateTime startTime, DateTime? endTime, string measuringPointId)
        {
            var response = DoGet("/api/v1/get_vdv_records", token,
                 startTime, endTime, measuringPointId).Result;
            return ParseJson<VdvRecords>(response);
        }

        public TracesListResponse GetTracesList(string token, string measuringPointId, DateTime startTime, DateTime? endTime)
        {
            var json = DoGet(path: "/api/v1/get_traces_list",
                               token: token,
                               measuringPointId: measuringPointId,
                               startTime: startTime,
                               endTime: endTime).Result;
            return ParseJson<TracesListResponse>(json);
        }

        public TracesReponse GetTraces(string token, string measuringPointId, DateTime startTime, DateTime? endTime)
        {
            var tracesJson = DoGet(path: "/api/v1/get_traces",
                                           token: token,
                                           measuringPointId: measuringPointId,
                                           startTime: startTime,
                                           endTime: endTime).Result;
            return ParseJson<TracesReponse>(tracesJson)!;
        }

        public OmnidotsResponse ConfigureMeasuringPoint(string token, string measuringPointId, string json)
        {
            var responsestring = DoConfigureMeasuringPoint(token, measuringPointId, json).Result;
            return ParseJson<OmnidotsResponse>(responsestring);
        }

        public async Task<OmnidotsResponse> ConfigureMeasuringPointAsync(
            string token,
            string measuringPointId,
            string json,
            CancellationToken cancellationToken)
        {
            var response = await DoConfigureMeasuringPoint(token, measuringPointId, json)
                .WaitAsync(cancellationToken);
            return ParseJson<OmnidotsResponse>(response);
        }

        private async Task<string> DoAuthenticate(MultipartFormDataContent content)
        {
            return await httpClient.PostAsync("/api/v1/user/authenticate", content);
        }

        private async Task<string> DoListMeasuringPoints(string token)
        {
            return await httpClient.GetAsync(string.Format("/api/v1/list_measuring_points?token={0}", token));
        }

        private async Task<string> DoGet(string path, string token,
                                         DateTime startTime, DateTime? endTime, string measuringPointId)
        {
            RvtLogger.Logger.LogDebug("DoGet path={Value1} startTime={Value2} endTime={Value3} measuringPointId={Value4}",
                                  path, startTime, endTime, measuringPointId);

            var sb = new StringBuilder(path)
              .Append("?token=")
              .Append(token)
              .Append("&measuring_point_id=")
              .Append(measuringPointId)
              .Append("&start_time=")
              .Append(DateTimeUtil.GetMillis(startTime));

            if (endTime != null)
            {
                sb.Append("&end_time=")
                .Append(DateTimeUtil.GetMillis((DateTime)endTime!));
            }
            var url = sb.ToString();
            var response = await httpClient.GetAsync(url);
            return response;
        }

        private async Task<string> DoConfigureMeasuringPoint(string token, string measuringPointId, string json)
        {
            var path = string.Format("/api/v1/configure_measuring_point?token={0}&measuring_point_id={1}",
                                     token, measuringPointId);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            return await httpClient.PostAsync(path, httpContent);
        }

        private static T ParseJson<T>(string json, bool isResponse = true)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json)!;
            }
            catch (JsonException e)
            {
                if (isResponse)
                {
                    RvtLogger.Logger.LogError(e, "Error parsing response JSON");

                    var errorResponse = ParseErrorResponse(json);
                    if (errorResponse != null)
                    {
                        throw AdapterException.Of("Failed ! error message='" + SensitiveLogRedactor.RedactJson(errorResponse.Message) + "'");
                    }
                    throw AdapterException.Of("Failed ! Invalid ErrorResponse", e);
                }
                else
                {
                    throw AdapterException.Of("Failed ! Could not parse json", e);
                }
            }
        }

        public static ErrorResponse? ParseErrorResponse(string response)
        {
            try
            {
                return JsonSerializer.Deserialize<ErrorResponse>(response)!;
            }
            catch (JsonException e)
            {
                RvtLogger.Logger.LogWarning(e, "Could not parse error message");
                return null;
            }
        }
    }
}
