
namespace Rvt.Monitor.Common.Diagnostics
{

    public class AdapterException : Exception
    {

        public static AdapterException Of(string message, Exception exception)
        {
            return new AdapterException(message, exception);
        }

        public static AdapterException Of(string message)
        {
            return new AdapterException(message);
        }

        public static AdapterException Of(string message, string errorResponse)
        {
            return new AdapterException(string.Format("{0}{1}", message, errorResponse));
        }

        private AdapterException()
        {
        }

        private AdapterException(string message)
            : base(message)
        {
        }

        private AdapterException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
