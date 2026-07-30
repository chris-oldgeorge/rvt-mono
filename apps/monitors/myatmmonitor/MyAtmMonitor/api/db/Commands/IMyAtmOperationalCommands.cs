namespace MyAtm.Api.Db
{
    public interface IMyAtmOperationalCommands
    {
        void HandleException(string message, Exception exception);

        void ClearErrorMessages(DateTime before);
    }
}
