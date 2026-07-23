namespace Rvt.Communication.Abstractions;

public class CommsException : Exception
{
    public string Address { get; }

    public static CommsException Of(string address, string message) => new(address, message);

    private CommsException(string address, string message)
        : base(message)
    {
        Address = address;
    }
}
