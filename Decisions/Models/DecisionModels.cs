using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIHappey.Decisions;

/// <summary>OpenAI-compatible request for POST /v1/decisions.</summary>
public sealed class OpenAIDecisionRequest
{
    [JsonPropertyName("input")]
    public OpenAIDecisionInput Input { get; set; } = null!;

    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    /// <summary>Questions and their answers retain their original order.</summary>
    [JsonPropertyName("questions")]
    public IReadOnlyList<OpenAIDecisionQuestion> Questions { get; set; } = [];

    [JsonPropertyName("prompt_cache_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PromptCacheKey { get; set; }

    [JsonPropertyName("prompt_cache_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIDecisionPromptCacheOptions? PromptCacheOptions { get; set; }

    /// <summary>Opaque caller-provided identifier, not the authenticated user identity.</summary>
    [JsonPropertyName("safety_identifier")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SafetyIdentifier { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>Text or ordered messages, tool calls, and tool outputs. Independent of Responses contracts.</summary>
[JsonConverter(typeof(OpenAIDecisionInputJsonConverter))]
public sealed class OpenAIDecisionInput
{
    public string? Text { get; }
    public IReadOnlyList<OpenAIDecisionInputItem>? Items { get; }

    /// <summary>Legacy message-only accessor. Throws rather than discarding tool calls or outputs.</summary>
    public IReadOnlyList<OpenAIDecisionInputMessage>? Messages => Items is null ? null
        : Items.All(item => item is OpenAIDecisionInputMessage) ? Items.Cast<OpenAIDecisionInputMessage>().ToArray()
        : throw new NotSupportedException("Decision input contains tool history; use Items instead of Messages.");

    public OpenAIDecisionInput(string text) => Text = text ?? throw new ArgumentNullException(nameof(text));

    public OpenAIDecisionInput(IEnumerable<OpenAIDecisionInputMessage> messages)
        : this((IEnumerable<OpenAIDecisionInputItem>)messages) { }

    public OpenAIDecisionInput(IEnumerable<OpenAIDecisionInputItem> items)
        => Items = [.. items ?? throw new ArgumentNullException(nameof(items))];

    public static implicit operator OpenAIDecisionInput(string text) => new(text);
    public static implicit operator OpenAIDecisionInput(OpenAIDecisionInputMessage[] messages) => new(messages);
    public static implicit operator OpenAIDecisionInput(OpenAIDecisionInputItem[] items) => new(items);
}

[JsonConverter(typeof(OpenAIDecisionInputItemJsonConverter))]
public abstract class OpenAIDecisionInputItem;

[JsonConverter(typeof(OpenAIDecisionInputMessageJsonConverter))]
public sealed class OpenAIDecisionInputMessage : OpenAIDecisionInputItem
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("phase")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phase { get; set; }

    [JsonPropertyName("content")]
    public OpenAIDecisionMessageContent Content { get; set; } = null!;
}

[JsonConverter(typeof(OpenAIDecisionMessageContentJsonConverter))]
public sealed class OpenAIDecisionMessageContent
{
    public string? Text { get; }
    public IReadOnlyList<OpenAIDecisionInputPart>? Parts { get; }

    public OpenAIDecisionMessageContent(string text) => Text = text ?? throw new ArgumentNullException(nameof(text));

    public OpenAIDecisionMessageContent(IEnumerable<OpenAIDecisionInputPart> parts)
        => Parts = [.. parts ?? throw new ArgumentNullException(nameof(parts))];

    public static implicit operator OpenAIDecisionMessageContent(string text) => new(text);
    public static implicit operator OpenAIDecisionMessageContent(OpenAIDecisionInputPart[] parts) => new(parts);
}

[JsonConverter(typeof(OpenAIDecisionInputPartJsonConverter))]
public abstract class OpenAIDecisionInputPart(string type)
{
    [JsonPropertyName("type")]
    public string Type { get; } = type;
}

public sealed class OpenAIDecisionInputText() : OpenAIDecisionInputPart("input_text")
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;

    [JsonPropertyName("prompt_cache_breakpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIDecisionPromptCacheBreakpoint? PromptCacheBreakpoint { get; set; }
}

/// <summary>
/// An inline data URL or publicly accessible HTTP(S) image URL. File IDs are not supported.
/// Representing an image does not fetch it or imply support in a particular backend.
/// </summary>
public sealed class OpenAIDecisionInputImage() : OpenAIDecisionInputPart("input_image")
{
    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; } = null!;

    /// <summary>low, high, auto, or original; omitted/null defaults to auto.</summary>
    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    [JsonPropertyName("prompt_cache_breakpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIDecisionPromptCacheBreakpoint? PromptCacheBreakpoint { get; set; }
}

[JsonConverter(typeof(OpenAIDecisionQuestionJsonConverter))]
public abstract class OpenAIDecisionQuestion(string type)
{
    [JsonPropertyName("type")]
    public string Type { get; } = type;

    /// <summary>Wire instructions: text or a nonempty array of text, image, or JSON parts.</summary>
    [JsonPropertyName("instructions")]
    public OpenAIDecisionQuestionInstructions InstructionContent { get; set; } = null!;

    /// <summary>Text-only convenience accessor retained for existing consumers. Never flattens parts.</summary>
    [JsonIgnore]
    public string Instructions
    {
        get => InstructionContent is null ? null! : InstructionContent.Text
            ?? throw new NotSupportedException("Multipart decision instructions require InstructionContent.Parts.");
        set => InstructionContent = value is null ? null! : new(value);
    }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}

public sealed class OpenAIDecisionPredicateQuestion() : OpenAIDecisionQuestion("predicate");

public sealed class OpenAIDecisionChoiceQuestion() : OpenAIDecisionQuestion("choice")
{
    [JsonPropertyName("choices")]
    public IReadOnlyList<OpenAIDecisionChoice> Choices { get; set; } = [];
}

public sealed class OpenAIDecisionScoreQuestion() : OpenAIDecisionQuestion("score")
{
    [JsonPropertyName("levels")]
    public IReadOnlyList<OpenAIDecisionScoreLevel> Levels { get; set; } = [];
}

/// <summary>A string or boolean. The string "true" and the boolean true remain distinct.</summary>
[JsonConverter(typeof(OpenAIDecisionChoiceValueJsonConverter))]
public readonly struct OpenAIDecisionChoiceValue
{
    public JsonElement Value { get; }

    public OpenAIDecisionChoiceValue(string value)
        => Value = JsonSerializer.SerializeToElement(value ?? throw new ArgumentNullException(nameof(value)));

    public OpenAIDecisionChoiceValue(bool value) => Value = JsonSerializer.SerializeToElement(value);

    public static implicit operator OpenAIDecisionChoiceValue(string value) => new(value);
    public static implicit operator OpenAIDecisionChoiceValue(bool value) => new(value);
}

public sealed class OpenAIDecisionChoice
{
    [JsonPropertyName("value")]
    public OpenAIDecisionChoiceValue Value { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

public sealed class OpenAIDecisionScoreLevel
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = null!;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

public sealed class OpenAIDecisionResponse
{
    [JsonPropertyName("answers")]
    public IReadOnlyList<OpenAIDecisionAnswer> Answers { get; set; } = [];

    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    [JsonPropertyName("usage")]
    public OpenAIDecisionUsage Usage { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

[JsonConverter(typeof(OpenAIDecisionAnswerJsonConverter))]
public abstract class OpenAIDecisionAnswer(string type)
{
    [JsonPropertyName("type")]
    public string Type { get; } = type;

    // Unlike question names, the answer name must be present even when null.
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Name { get; set; }
}

public sealed class OpenAIDecisionPredicateAnswer() : OpenAIDecisionAnswer("predicate")
{
    [JsonPropertyName("probability")]
    public double Probability { get; set; }
}

public sealed class OpenAIDecisionChoiceAnswer() : OpenAIDecisionAnswer("choice")
{
    [JsonPropertyName("choice")]
    public OpenAIDecisionChoiceValue Choice { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("probabilities")]
    public IReadOnlyList<OpenAIDecisionChoiceProbability> Probabilities { get; set; } = [];
}

public sealed class OpenAIDecisionScoreAnswer() : OpenAIDecisionAnswer("score")
{
    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("probabilities")]
    public IReadOnlyList<OpenAIDecisionScoreProbability> Probabilities { get; set; } = [];
}

public sealed class OpenAIDecisionRefusalAnswer() : OpenAIDecisionAnswer("refusal");

public sealed class OpenAIDecisionChoiceProbability
{
    [JsonPropertyName("value")]
    public OpenAIDecisionChoiceValue Value { get; set; }

    [JsonPropertyName("probability")]
    public double Probability { get; set; }
}

public sealed class OpenAIDecisionScoreProbability
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = null!;

    [JsonPropertyName("value")]
    // The supplied OpenAI reference defines integer, zero-based level indices.
    // Double is retained deliberately for existing compatible providers with fractional scales.
    public double Value { get; set; }

    [JsonPropertyName("probability")]
    public double Probability { get; set; }
}

public sealed class OpenAIDecisionUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("input_tokens_details")]
    public OpenAIDecisionInputTokenDetails InputTokensDetails { get; set; } = new();

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("output_tokens_details")]
    public OpenAIDecisionOutputTokenDetails OutputTokensDetails { get; set; } = new();

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public sealed class OpenAIDecisionInputTokenDetails
{
    [JsonPropertyName("cache_write_tokens")]
    public int CacheWriteTokens { get; set; }

    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }
}

public sealed class OpenAIDecisionOutputTokenDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}
