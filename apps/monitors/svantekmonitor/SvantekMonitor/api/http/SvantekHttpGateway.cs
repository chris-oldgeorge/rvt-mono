using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Rvt.Monitor.Common.Configuration;
using Rvt.Monitor.Common.Diagnostics;
using Svantek.Model.Http;

namespace Svantek.Api.Http
{
    // Summary: Vendor HTTP gateway for the SvanNET API - request building, calls, and response parsing.
    // Major updates:
    // - 2026-07-12 God-class split: extracted from the SvantekApi partials (SvantekApiProjects, SvantekApi).
    public class SvantekHttpGateway
    {
        private const string API_URL_PROJECTS_GET_DATA = "projects-get-data.php";
        private const string API_URL_STATIONS_GET_LIST = "stations-get-list.php";
        private const string API_URL_PROJECTS_GET_RESULT_DATA_MULTI = "projects-get-result-data-multi-point.php";

        private readonly IHttpClient httpClient;
        private readonly string apiKey;

        public SvantekHttpGateway(IHttpClient httpClient, string apiKey)
        {
            this.httpClient = httpClient;
            this.apiKey = apiKey;
        }

        public async Task<List<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
        {
            string response;

            try
            {
                response = await CallApiProjectsGetDataAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("GetProjects", e);
            }
            ProjectsResponse apiResponse = ParseResponse<ProjectsResponse>(response);
            if (apiResponse.status != "ok") throw new Exception("GetProjects ProjectsResponse status " + apiResponse.status);
            return apiResponse.projects;
        }

        public async Task<List<ProjectFile>> GetProjectFilesAsync(
            string projectId,
            string pointId,
            string? dayCode = null,
            string? filename = null,
            CancellationToken cancellationToken = default)
        {
            string response;

            try
            {
                response = await CallApiProjectsGetDataAsync(
                    projectId,
                    pointId,
                    dayCode,
                    filename,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("GetProjectFiles", e);
            }

            return ParseResponse<ProjectFilesResponse>(response).files;
        }

        public async Task<List<Station>> GetStationsAsync(CancellationToken cancellationToken = default)
        {
            string response;

            try
            {
                response = await CallApiStationsGetListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("GetStations", e);
            }
            StationsResponse apiResponse = ParseResponse<StationsResponse>(response);
            if (apiResponse.status != "ok") throw new Exception("StationsResponse status " + apiResponse.status);
            return apiResponse.stations;
        }

        public async Task<List<MultiData>> GetDataMultiAsync(
            string projectId,
            IList<MultiDataArgument> arguments,
            CancellationToken cancellationToken = default)
        {
            string response;

            try
            {
                response = await CallApiProjectsGetResultDataMultiAsync(
                    projectId,
                    arguments,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("GetDataMulti", e);
            }

            MultiDataResponse apiResponse = ParseResponse<MultiDataResponse>(response);
            if (apiResponse.status != "ok") throw new Exception(string.Format("MultiDataResponse status {0} project {1}", apiResponse.status, projectId));
            return apiResponse.data;
        }

        public async Task<byte[]> GetSoundFileAsync(
            int project,
            int point,
            string stationType,
            string daycode,
            string serialId,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            using var content = new MultipartFormDataContent();
            var values = new[]
            {
                new KeyValuePair<string, string>("key", apiKey),
                new KeyValuePair<string, string>("project", project.ToString()),
                new KeyValuePair<string, string>("point", point.ToString()),
                new KeyValuePair<string, string>("station_type", stationType),
                new KeyValuePair<string, string>("day_code", daycode),
                new KeyValuePair<string, string>("station_serial", serialId),
                new KeyValuePair<string, string>("filename", fileName)
            };

            foreach (var keyValuePair in values)
            {
                content.Add(new StringContent(keyValuePair.Value),
                    String.Format("\"{0}\"", keyValuePair.Key));
            }

            try
            {
                return await httpClient.GetByteArrayAsync(
                    API_URL_PROJECTS_GET_DATA,
                    content,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw AdapterException.Of("GetSoundFile", e);
            }
        }

        #region ApiCalls

        private async Task<string> CallApiProjectsGetDataAsync(
            string? projectId = null,
            string? pointId = null,
            string? dayCode = null,
            string? filename = null,
            CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key", apiKey)
            };

            if (projectId != null)
            {
                values.Add(new KeyValuePair<string, string>("project", projectId));
            }
            if (pointId != null)
            {
                values.Add(new KeyValuePair<string, string>("point", pointId));
            }
            if (dayCode != null)
            {
                values.Add(new KeyValuePair<string, string>("day_code", dayCode));
            }
            if (filename != null)
            {
                values.Add(new KeyValuePair<string, string>("filename", filename));
            }

            using var content = GetApiContent(values);
            return await httpClient.PostAsync(
                API_URL_PROJECTS_GET_DATA,
                content,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> CallApiStationsGetListAsync(
            string? stationId = null,
            CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key", apiKey)
            };

            if (stationId != null)
            {
                values.Add(new KeyValuePair<string, string>("station", stationId));
            }

            using var content = GetApiContent(values);
            return await httpClient.PostAsync(
                API_URL_STATIONS_GET_LIST,
                content,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> CallApiProjectsGetResultDataMultiAsync(
            string projectId,
            IList<MultiDataArgument> arguments,
            CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key", apiKey),
                new KeyValuePair<string, string>("project", projectId),
                new KeyValuePair<string, string>("results", "[\"leq-S-S-0-0-N\",\"max-S-S-0-0-N\",\"l90-S-S-0-0-N\",\"l10-S-S-0-0-N\",\"leq-S-S-0-1-N\",\"max-S-S-0-1-N\",\"l90-S-S-0-1-N\",\"l10-S-S-0-1-N\"]"),// LAeq, LCeq, L10 , L90
                new KeyValuePair<string, string>("data", JsonSerializer.Serialize(arguments)),
            };

            using var content = GetApiContent(values);
            return await httpClient.PostAsync(
                API_URL_PROJECTS_GET_RESULT_DATA_MULTI,
                content,
                cancellationToken).ConfigureAwait(false);
        }

        #endregion // ApiCalls

        private static MultipartFormDataContent GetApiContent(List<KeyValuePair<string, string>> values)
        {
            var content = new MultipartFormDataContent();

            foreach (var keyValuePair in values)
            {
                content.Add(new StringContent(keyValuePair.Value), $"\"{keyValuePair.Key}\"");
            }

            return content;
        }

        private static T ParseResponse<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json)!;
            }
            catch (JsonException e)
            {
                // could be an error response
                var errors = ParseErrorResponse(json);
                if (errors != null && errors.Count > 0)
                {
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
    }
}
