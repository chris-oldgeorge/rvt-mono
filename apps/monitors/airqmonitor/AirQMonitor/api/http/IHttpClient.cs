namespace AirQ.Api.Http
{

    public interface IHttpClient
    {
        public Task<string> GetAsync(string path);

    }

}
