namespace Rvt.Communication.MicrosoftGraphMail;

public interface IMicrosoftGraphAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
