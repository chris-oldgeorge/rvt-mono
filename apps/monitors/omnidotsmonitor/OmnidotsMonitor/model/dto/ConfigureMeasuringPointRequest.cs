using System.Text.Json.Serialization;

namespace Omnidots.Model.Dto;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ConfigureMeasuringPointRequest
{
    [JsonPropertyName("secret")]
    public string? Secret { get; init; }

    [JsonPropertyName("serialid")]
    public string? SerialId { get; init; }

    [JsonPropertyName("trace_save_level")]
    public double? TraceSaveLevel { get; init; }

    [JsonPropertyName("trace_pre_trigger")]
    public double? TracePreTrigger { get; init; }

    [JsonPropertyName("trace_post_trigger")]
    public double? TracePostTrigger { get; init; }

    [JsonPropertyName("flat_level")]
    public double? FlatLevel { get; init; }

    [JsonPropertyName("level_alert")]
    public double? LevelAlert { get; init; }

    [JsonPropertyName("level_caution")]
    public double? LevelCaution { get; init; }
}
