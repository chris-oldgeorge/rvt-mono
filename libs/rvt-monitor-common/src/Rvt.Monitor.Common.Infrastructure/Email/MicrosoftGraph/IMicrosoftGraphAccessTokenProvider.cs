namespace Rvt.Monitor.Common.Infrastructure.Email.MicrosoftGraph;

public interface IMicrosoftGraphAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
