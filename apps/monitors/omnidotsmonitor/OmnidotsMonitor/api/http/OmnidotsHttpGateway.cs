using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omnidots.Api.Ports;
using Omnidots.Model.Json;
using Rvt.Monitor.Common.Diagnostics;
using Rvt.Monitor.Common.Utilities;

namespace Omnidots.Api.Http;

// Summary: Vendor HTTP gateway for the Omnidots Honeycomb API - authentication, calls, and response parsing.
// Major updates:
// - 2026-07-12 God-class split: extracted from the OmnidotsApi partials (OmnidotsApi, OmnidotsApiMonitors, OmnidotsApiVibrationLevels, OmnidotsApiTraces, OmnidotsApiConfiguration).
public class OmnidotsHttpGateway(IHttpClient httpClient, string userId, string userAuth) : IOmnidotsVendorGateway
{
    private readonly IHttpClient httpClient = httpClient;
    private readonly string userId = userId;
    private readonly string userAuth = userAuth;

    public async Task<TokenResponse> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        using MultipartFormDataContent content = new();
        KeyValuePair<string, string>[] values =
        [
            new KeyValuePair<string, string>("username", userId),
            new KeyValuePair<string, string>("password", userAuth)
        ];

        foreach (KeyValuePair<string, string> keyValuePair in values)
        {
            content.Add(new StringContent(keyValuePair.Value),
                String.Format("\"{0}\"", keyValuePair.Key));
        }

        // The token flows into the request itself rather than being awaited
        // around it, so a shutdown cancels the call instead of abandoning it.
        string response = await DoAuthenticate(content, cancellationToken);
        RvtLogger.Logger.LogDebug("Authenticate response={Value1}", SensitiveLogRedactor.RedactJson(response));
        return ParseJson<TokenResponse>(response);
    }

    public async Task<MeasuringPointsResponse> ListMeasuringPointsAsync(CancellationToken cancellationToken = default)
    {
        TokenResponse authentication = await AuthenticateAsync(cancellationToken);
        string response = await DoListMeasuringPoints(authentication.Token!, cancellationToken);
        return ParseJson<MeasuringPointsResponse>(response);
    }

    public async Task<PeakRecords> GetPeakRecordsAsync(string token, DateTime startTime, DateTime? endTime, string measuringPointId, CancellationToken cancellationToken = default)
    {
        string response = await DoGet(path: "/api/v1/get_peak_records", token: token,
                                 startTime: startTime, endTime: endTime, measuringPointId: measuringPointId,
                                 cancellationToken: cancellationToken);
        return ParseJson<PeakRecords>(response);
    }

    public async Task<VeffRecords> GetVeffRecordsAsync(string token, DateTime startTime, DateTime? endTime, string measuringPointId, CancellationToken cancellationToken = default)
    {
        string response = await DoGet("/api/v1/get_veff_records", token,
             startTime, endTime, measuringPointId, cancellationToken);
        return ParseJson<VeffRecords>(response);
    }

    public async Task<VdvRecords> GetVdvRecordsAsync(string token, DateTime startTime, DateTime? endTime, string measuringPointId, CancellationToken cancellationToken = default)
    {
        string response = await DoGet("/api/v1/get_vdv_records", token,
             startTime, endTime, measuringPointId, cancellationToken);
        return ParseJson<VdvRecords>(response);
    }

    public async Task<TracesListResponse> GetTracesListAsync(string token, string measuringPointId, DateTime startTime, DateTime? endTime, CancellationToken cancellationToken = default)
    {
        string json = await DoGet(path: "/api/v1/get_traces_list",
                           token: token,
                           measuringPointId: measuringPointId,
                           startTime: startTime,
                           endTime: endTime,
                           cancellationToken: cancellationToken);
        return ParseJson<TracesListResponse>(json);
    }

    public async Task<TracesReponse> GetTracesAsync(string token, string measuringPointId, DateTime startTime, DateTime? endTime, CancellationToken cancellationToken = default)
    {
        string tracesJson = await DoGet(path: "/api/v1/get_traces",
                                       token: token,
                                       measuringPointId: measuringPointId,
                                       startTime: startTime,
                                       endTime: endTime,
                                       cancellationToken: cancellationToken);
        return ParseJson<TracesReponse>(tracesJson)!;
    }

    public async Task<OmnidotsResponse> ConfigureMeasuringPointAsync(
        string token,
        string measuringPointId,
        string json,
        CancellationToken cancellationToken = default)
    {
        string response = await DoConfigureMeasuringPoint(token, measuringPointId, json, cancellationToken);
        return ParseJson<OmnidotsResponse>(response);
    }

    private async Task<string> DoAuthenticate(MultipartFormDataContent content, CancellationToken cancellationToken)
    {
        return await httpClient.PostAsync("/api/v1/user/authenticate", content, cancellationToken);
    }

    private async Task<string> DoListMeasuringPoints(string token, CancellationToken cancellationToken)
    {
        return await httpClient.GetAsync(string.Format("/api/v1/list_measuring_points?token={0}", token), cancellationToken);
    }

    private async Task<string> DoGet(string path, string token,
                                     DateTime startTime, DateTime? endTime, string measuringPointId,
                                     CancellationToken cancellationToken)
    {
        RvtLogger.Logger.LogDebug("DoGet path={Value1} startTime={Value2} endTime={Value3} measuringPointId={Value4}",
                              path, startTime, endTime, measuringPointId);

        StringBuilder sb = new StringBuilder(path)
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
        string url = sb.ToString();
        string response = await httpClient.GetAsync(url, cancellationToken);
        return response;
    }

    private async Task<string> DoConfigureMeasuringPoint(string token, string measuringPointId, string json, CancellationToken cancellationToken)
    {
        string path = string.Format("/api/v1/configure_measuring_point?token={0}&measuring_point_id={1}",
                                 token, measuringPointId);
        StringContent httpContent = new(json, Encoding.UTF8, "application/json");
        return await httpClient.PostAsync(path, httpContent, cancellationToken);
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

                ErrorResponse? errorResponse = ParseErrorResponse(json);
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
