namespace AirQ.Api.Http
{

    /// <summary>
    /// Driven port for raw vendor HTTP access. Cancellation is part of the
    /// contract so a shutdown signal reaches the in-flight request instead of
    /// being discarded at the job boundary.
    /// </summary>
    public interface IHttpClient
    {
        public Task<string> GetAsync(string path, CancellationToken cancellationToken = default);

    }
}
