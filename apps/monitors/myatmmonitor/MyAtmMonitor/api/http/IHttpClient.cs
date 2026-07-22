namespace MyAtm.Api.Http
{

    public interface IHttpClient
    {
        Task<string> GetAsync(string path, CancellationToken cancellationToken = default);
    }
}
