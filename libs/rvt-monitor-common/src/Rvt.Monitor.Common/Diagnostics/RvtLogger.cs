using Microsoft.Extensions.Logging;

namespace Rvt.Monitor.Common.Diagnostics
{

    public class RvtLogger
    {
        private static readonly object mutex = new();
        private static RvtLogger? instance;

        private readonly ILogger logger;

        private RvtLogger(ILoggerFactory loggerFactory, string categoryName)
        {
            logger = loggerFactory.CreateLogger(categoryName);
        }

        public static void CreateLogger(ILoggerFactory loggerFactory, string categoryName)
        {

            lock (mutex)
            {
                instance = new RvtLogger(loggerFactory, categoryName);
            }
        }

        public static ILogger Logger
        {
            get
            {
                lock (mutex)
                {
                    if (instance == null)
                    {
                        throw AdapterException.Of("MyAtmLogger logger not yet created");
                    }
                    return instance.logger;
                }
            }
        }
    }

}

