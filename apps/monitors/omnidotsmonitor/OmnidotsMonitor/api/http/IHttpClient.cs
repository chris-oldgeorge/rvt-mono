namespace Omnidots.Api.Http
{

    public interface IHttpClient
    {
        public Task<string> GetAsync(string path);
        public Task<string> PostAsync(string path, HttpContent content);

    }
}
