// File summary: Writes an application CSV download straight to the response body as an attachment.
// Major updates:
// - 2026-07-30 pending Added when CSV exports stopped being buffered into a string and its UTF-8 copy.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RvtPortal.Spa.UseCases.Data;

namespace RvtPortal.Spa.Api;

/// <summary>
/// The transport half of a streamed CSV export. <see cref="FileContentResult"/> needs the whole body up front,
/// which is exactly what the buffered export was doing: an export bounded only by the reader's million-row cap
/// was held as one string and then copied into an equal-sized UTF-8 array before a single byte reached the
/// client. This writes the rows to the response body as they are formatted.
/// <para>
/// The headers are deliberately the ones <c>FileResultExecutorBase</c> emits for
/// <c>File(bytes, contentType, fileName)</c> - <c>attachment</c> with both <c>filename</c> and
/// <c>filename*</c> - so the change is invisible on the wire. Content-Length is the one difference: the length
/// is not known in advance, so the response is chunked.
/// </para>
/// </summary>
internal sealed class CsvDownloadResult : IActionResult
{
    private readonly DataDownloadModel _download;

    // Function summary: Initializes this result with the download the application service described.
    public CsvDownloadResult(DataDownloadModel download)
    {
        _download = download;
    }

    // Function summary: Writes the download's content type, attachment headers, and streamed body.
    public async Task ExecuteResultAsync(ActionContext context)
    {
        HttpResponse response = context.HttpContext.Response;
        response.ContentType = _download.ContentType;
        ContentDispositionHeaderValue contentDisposition = new("attachment");
        contentDisposition.SetHttpFileName(_download.FileName);
        response.Headers.ContentDisposition = contentDisposition.ToString();

        await _download.WriteAsync(response.Body, context.HttpContext.RequestAborted);
    }
}
