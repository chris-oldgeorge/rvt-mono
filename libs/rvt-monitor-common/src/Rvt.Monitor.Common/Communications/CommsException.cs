
namespace Rvt.Monitor.Common.Communications
{

    public class CommsException : Exception
    {
        public string Address { get; }

        public static CommsException Of(string address, string message)
        {
            return new CommsException(address, message);
        }


        private CommsException(string address, string message)
            : base(message)
        {
            Address = address;
        }

    }
}
