using System.Collections.Specialized;
using System.Web;
using Rvt.Monitor.Common.Diagnostics;

namespace Omnidots.Api;


public sealed class OmnidotsQueryProcessor
{
    private OmnidotsQueryProcessor() { }

    public static int GetIntParameter(string query, string name)
    {
        NameValueCollection map = HttpUtility.ParseQueryString(query);
        try
        {
            return int.Parse(map[name]!);
        }
        catch (FormatException e)
        {
            throw AdapterException.Of("Failed ! " + name + " must be an Integer", e);
        }
    }

    public static string GetStringParameter(string query, string name)
    {
        NameValueCollection map = HttpUtility.ParseQueryString(query);
        return map[name]!;
    }
}
