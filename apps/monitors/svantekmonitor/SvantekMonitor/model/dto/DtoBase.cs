using System.Reflection;
using System.Text;

namespace SvantekMonitor.model.dto
{
    // Summary: Shared diagnostic base for Svantek DTOs that prints public property values.
    // Major updates:
    // - 2026-06-18: Renamed from lowercase dto to DtoBase and made reflection null-safe.
    public class DtoBase
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (PropertyInfo prop in this.GetType().GetProperties())
            {
                string propName = prop.Name;
                object? propValue = prop.GetValue(this, null);
                sb.AppendLine($"{propName}: {propValue}");
            }
            return sb.ToString();
        }
    }
}
