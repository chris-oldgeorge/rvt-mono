namespace Omnidots.Api.Db;

public interface IOmnidotsOperationalCommands
{
    void HandleException(string message, Exception exception);

    void ClearErrorMessages(DateTime before);
}
