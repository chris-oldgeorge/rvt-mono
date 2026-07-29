// The namespace follows this project's established scheme rather than the
// folder path; IDE0130 would require a name no sibling file uses.
#pragma warning disable IDE0130
using System.Text.Json;
using Omnidots.Model.Json;

namespace Omnidots.Api.Http;

// Summary: Composition-selected decorator that answers the vendor authenticate
// call with the configured static token instead of calling the vendor.
// Major updates:
// - 2026-07-29 Vendor transport consolidation: moved out of HttpWebClient's
//   request path, where every POST paid for the check and the transport
//   could silently fake responses. The composition root now decides whether
//   this seam exists at all; the transport underneath is honest.
public sealed class OmnidotsStaticTokenClient : IHttpClient
{
    private const string _authenticatePathPrefix = "/api/v1/user/authenticate";

    private readonly IHttpClient _inner;
    private readonly string _token;

    public OmnidotsStaticTokenClient(IHttpClient inner, string token)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _token = token;
    }

    public Task<string> GetAsync(string path, CancellationToken cancellationToken = default) =>
        _inner.GetAsync(path, cancellationToken);

    public Task<string> PostAsync(string path, HttpContent content, CancellationToken cancellationToken = default)
    {
        if (path.StartsWith(_authenticatePathPrefix, StringComparison.Ordinal))
        {
            TokenResponse response = new()
            {
                Ok = true,
                Token = _token
            };
            return Task.FromResult(JsonSerializer.Serialize(response));
        }

        return _inner.PostAsync(path, content, cancellationToken);
    }
}
