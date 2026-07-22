// File summary: Streams archive query rows into RFC 4180-compatible CSV files.
// Major updates:
// - 2026-07-09 pending Added streaming CSV writer for site archive exports.

using System.Reflection;

namespace RvtPortal.Spa.Adapters.Archive;

internal interface ISiteArchiveCsvWriter
{
    // Function summary: Streams typed archive rows into a CSV file without materializing the full result set.
    Task WriteAsync<T>(string filePath, IAsyncEnumerable<T> rows, CancellationToken cancellationToken)
        where T : class;
}

internal sealed class SiteArchiveCsvWriter : ISiteArchiveCsvWriter
{
    private const string Separator = ",";

    // Function summary: Writes a CSV header and streams escaped row values to disk.
    public async Task WriteAsync<T>(string filePath, IAsyncEnumerable<T> rows, CancellationToken cancellationToken)
        where T : class
    {
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(fileStream);

        await writer.WriteLineAsync(string.Join(Separator, properties.Select(property => EscapeCsvField(property.Name))));
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            var values = properties.Select(property => EscapeCsvField(property.GetValue(row)));
            await writer.WriteLineAsync(string.Join(Separator, values));
        }
    }

    // Function summary: Escapes a CSV field per RFC 4180 and neutralizes spreadsheet formula injection.
    private static string EscapeCsvField(object? raw)
    {
        var field = raw?.ToString() ?? "";
        if (field.Length > 0 && "=+-@\t\r".Contains(field[0]))
        {
            field = "'" + field;
        }

        if (field.IndexOfAny(['"', ',', '\n', '\r']) >= 0)
        {
            field = "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return field;
    }
}
