using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace AIHappey.Core.Models;

/// <summary>
/// OpenAI compatible request DTO for <c>POST /v1/audio/transcriptions</c>.
/// <para>
/// This is intentionally separate from the Vercel AI SDK transcription DTO.
/// Only fields that can be bridged to the current Vercel-compatible provider
/// contract are modeled here.
/// </para>
/// </summary>
public class OpenAITranscriptionRequest
{
    [JsonPropertyName("file")]
    [JsonIgnore]
    public IFormFile File { get; set; } = null!;

    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    [JsonPropertyName("prompt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prompt { get; set; }

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseFormat { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; set; }

    [JsonPropertyName("timestamp_granularities")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? TimestampGranularities { get; set; }

    [JsonPropertyName("stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stream { get; set; }

    [JsonPropertyName("include")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Include { get; set; }

    [JsonPropertyName("chunking_strategy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ChunkingStrategy { get; set; }

    [JsonPropertyName("known_speaker_names")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? KnownSpeakerNames { get; set; }

    [JsonPropertyName("known_speaker_references")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? KnownSpeakerReferences { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class AudioTranscriptionServerVadChunkingStrategy
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "server_vad";

    [JsonPropertyName("prefix_padding_ms")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PrefixPaddingMs { get; set; }

    [JsonPropertyName("silence_duration_ms")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SilenceDurationMs { get; set; }
}
