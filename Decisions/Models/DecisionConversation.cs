using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIHappey.Decisions;

/// <summary>Structured evidence, not executable message fields. Data is an object or array of objects.</summary>
public sealed class OpenAIDecisionInputJson() : OpenAIDecisionInputPart("input_json")
{
    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }
}

public sealed class OpenAIDecisionOutputText() : OpenAIDecisionInputPart("output_text")
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;
}

public sealed class OpenAIDecisionInputRefusal() : OpenAIDecisionInputPart("refusal")
{
    [JsonPropertyName("refusal")]
    public string Refusal { get; set; } = null!;
}

public abstract class OpenAIDecisionToolItem : OpenAIDecisionInputItem
{
    [JsonPropertyName("call_id")]
    public string CallId { get; set; } = null!;

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }
}

public abstract class OpenAIDecisionToolCall : OpenAIDecisionToolItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("namespace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Namespace { get; set; }
}

public sealed class OpenAIDecisionFunctionCall : OpenAIDecisionToolCall
{
    [JsonPropertyName("type")]
    public string Type => "function_call";

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = null!;
}

public sealed class OpenAIDecisionCustomToolCall : OpenAIDecisionToolCall
{
    [JsonPropertyName("type")]
    public string Type => "custom_tool_call";

    [JsonPropertyName("input")]
    public string Input { get; set; } = null!;
}

public abstract class OpenAIDecisionToolOutput : OpenAIDecisionToolItem
{
    /// <summary>Text or input_text/input_image parts only, associated with a matching call ID.</summary>
    [JsonPropertyName("output")]
    public OpenAIDecisionMessageContent Output { get; set; } = null!;
}

public sealed class OpenAIDecisionFunctionCallOutput : OpenAIDecisionToolOutput
{
    [JsonPropertyName("type")]
    public string Type => "function_call_output";
}

public sealed class OpenAIDecisionCustomToolCallOutput : OpenAIDecisionToolOutput
{
    [JsonPropertyName("type")]
    public string Type => "custom_tool_call_output";
}

[JsonConverter(typeof(OpenAIDecisionQuestionInstructionsJsonConverter))]
public sealed class OpenAIDecisionQuestionInstructions
{
    public string? Text { get; }
    public IReadOnlyList<OpenAIDecisionInputPart>? Parts { get; }

    public OpenAIDecisionQuestionInstructions(string text)
        => Text = text ?? throw new ArgumentNullException(nameof(text));

    public OpenAIDecisionQuestionInstructions(IEnumerable<OpenAIDecisionInputPart> parts)
    {
        Parts = [.. parts ?? throw new ArgumentNullException(nameof(parts))];
        DecisionContentRules.ValidateQuestionParts(Parts);
    }

    public static implicit operator OpenAIDecisionQuestionInstructions(string text) => new(text);
    public static implicit operator OpenAIDecisionQuestionInstructions(OpenAIDecisionInputPart[] parts) => new(parts);
}

public sealed class OpenAIDecisionPromptCacheOptions
{
    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; set; }

    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ttl { get; set; }
}

public sealed class OpenAIDecisionPromptCacheBreakpoint
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "explicit";
}
