namespace Svantek.Api.Http
{
    public interface IHttpClient
    {
        Task<string> GetAsync(string path, CancellationToken cancellationToken = default);
        Task<string> PostAsync(string path, HttpContent content, CancellationToken cancellationToken = default);
        Task<byte[]> GetByteArrayAsync(string path, MultipartFormDataContent content, CancellationToken cancellationToken = default);
    }
}
