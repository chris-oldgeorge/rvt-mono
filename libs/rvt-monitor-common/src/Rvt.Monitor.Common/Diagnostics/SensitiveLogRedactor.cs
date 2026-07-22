using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Rvt.Monitor.Common.Diagnostics;

public static class SensitiveLogRedactor
{
    private const int VisiblePrefixLength = 4;
    private static readonly Regex SensitiveAssignmentPattern = new(
        @"(?<name>\b(?:token|secret|password|api[_-]?key|user[_-]?(?:auth|id)|authorization|key|username)\b\s*[:=]\s*)(?<value>[^\s,&;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(empty)";
        }

        return value[..Math.Min(VisiblePrefixLength, value.Length)] + "****";
    }

    public static string RedactUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return "(empty)";
        }

        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0)
        {
            return url;
        }

        var path = url[..queryIndex];
        var query = url[(queryIndex + 1)..];
        var redactedQuery = string.Join("&", query.Split('&').Select(RedactQueryPart));
        return path + "?" + redactedQuery;
    }

    public static string RedactJson(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return "(empty)";
        }

        try
        {
            var node = JsonNode.Parse(payload);
            if (node is null)
            {
                return RedactSensitiveAssignments(payload);
            }

            RedactSensitiveProperties(node);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return RedactSensitiveAssignments(payload);
        }
    }

    private static string RedactQueryPart(string part)
    {
        var separatorIndex = part.IndexOf('=');
        if (separatorIndex < 0)
        {
            return IsSensitiveName(part) ? part + "=****" : part;
        }

        var name = part[..separatorIndex];
        return IsSensitiveName(name)
            ? name + "=" + Redact(part[(separatorIndex + 1)..])
            : part;
    }

    private static void RedactSensitiveProperties(JsonNode node)
    {
        if (node is JsonObject objectNode)
        {
            foreach (var property in objectNode.ToList())
            {
                if (IsSensitiveName(property.Key))
                {
                    objectNode[property.Key] = Redact(GetPropertyValue(property.Value));
                    continue;
                }

                if (property.Value is not null)
                {
                    RedactSensitiveProperties(property.Value);
                }
            }

            return;
        }

        if (node is JsonArray arrayNode)
        {
            foreach (var item in arrayNode)
            {
                if (item is not null)
                {
                    RedactSensitiveProperties(item);
                }
            }
        }
    }

    private static string GetPropertyValue(JsonNode? value)
    {
        if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            return text;
        }

        return value?.ToJsonString() ?? string.Empty;
    }

    private static string RedactSensitiveAssignments(string value) =>
        SensitiveAssignmentPattern.Replace(value, match =>
            match.Groups["name"].Value + Redact(match.Groups["value"].Value));

    private static bool IsSensitiveName(string name)
    {
        var normalizedName = name.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalizedName.Contains("token", StringComparison.Ordinal) ||
               normalizedName.Contains("secret", StringComparison.Ordinal) ||
               normalizedName.Contains("password", StringComparison.Ordinal) ||
               normalizedName is "key" or "apikey" or "authorization" or "userauth" or "userid" or "username";
    }
}
