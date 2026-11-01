using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;

namespace AIHappey.Decisions;

public sealed class OpenAIDecisionInputItemJsonConverter : JsonConverter<OpenAIDecisionInputItem>
{
    public override OpenAIDecisionInputItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Decision input items must be objects.");
        var type = root.TryGetProperty("type", out var discriminator)
            ? discriminator.ValueKind == JsonValueKind.String ? discriminator.GetString() : throw new JsonException("Input item type must be a string.")
            : "message";
        var variant = type switch
        {
            "message" => typeof(OpenAIDecisionInputMessage),
            "function_call" => typeof(OpenAIDecisionFunctionCall),
            "function_call_output" => typeof(OpenAIDecisionFunctionCallOutput),
            "custom_tool_call" => typeof(OpenAIDecisionCustomToolCall),
            "custom_tool_call_output" => typeof(OpenAIDecisionCustomToolCallOutput),
            _ => throw new JsonException($"Unsupported decision input item type '{type}'.")
        };
        var item = (OpenAIDecisionInputItem)(root.Deserialize(variant, options)
            ?? throw new JsonException("Decision input items cannot be null."));
        DecisionContentRules.ValidateItem(item);
        return item;
    }

    public override void Write(Utf8JsonWriter writer, OpenAIDecisionInputItem value, JsonSerializerOptions options)
    {
        DecisionContentRules.ValidateItem(value);
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

public sealed class OpenAIDecisionQuestionInstructionsJsonConverter : JsonConverter<OpenAIDecisionQuestionInstructions>
{
    public override OpenAIDecisionQuestionInstructions Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => new(reader.GetString()!),
            JsonTokenType.StartArray => new(JsonSerializer.Deserialize<OpenAIDecisionInputPart[]>(ref reader, options)!),
            _ => throw new JsonException("Decision question instructions must be text or a nonempty array of content parts.")
        };

    public override void Write(Utf8JsonWriter writer, OpenAIDecisionQuestionInstructions value, JsonSerializerOptions options)
    {
        if (value.Text is not null)
            writer.WriteStringValue(value.Text);
        else
        {
            DecisionContentRules.ValidateQuestionParts(value.Parts!);
            JsonSerializer.Serialize(writer, value.Parts, options);
        }
    }
}

internal static class DecisionContentRules
{
    private static readonly JsonSerializerOptions CompactJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static void ValidateHistory(IReadOnlyList<OpenAIDecisionInputItem>? items)
    {
        if (items is null) return;
        var functions = new HashSet<string>(StringComparer.Ordinal);
        var customTools = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null) throw new JsonException("Decision input items cannot be null.");
            ValidateItem(item);
            switch (item)
            {
                case OpenAIDecisionFunctionCall call: functions.Add(call.CallId); break;
                case OpenAIDecisionCustomToolCall call: customTools.Add(call.CallId); break;
                case OpenAIDecisionFunctionCallOutput output when !functions.Contains(output.CallId):
                case OpenAIDecisionCustomToolCallOutput custom when !customTools.Contains(custom.CallId):
                    throw new JsonException("Decision tool outputs require a matching earlier call of the same kind.");
            }
        }
    }

    internal static void ValidateMessage(OpenAIDecisionInputMessage message)
    {
        if (message.Type is not (null or "message") || message.Role is not ("user" or "developer" or "system" or "assistant"))
            throw new JsonException("Decision messages require a supported role and optional type 'message'.");
        if (message.Phase is not null && (message.Role != "assistant" || message.Phase is not ("commentary" or "final_answer")))
            throw new JsonException("Only assistant messages support phase 'commentary' or 'final_answer'.");
        if (message.Content is null)
            throw new JsonException("Decision messages require content.");
        foreach (var part in message.Content.Parts ?? [])
        {
            var allowed = message.Role switch
            {
                "user" => part is OpenAIDecisionInputText or OpenAIDecisionInputImage or OpenAIDecisionInputJson,
                "assistant" => part is OpenAIDecisionOutputText or OpenAIDecisionInputRefusal,
                _ => part is OpenAIDecisionInputText
            };
            if (!allowed) throw new JsonException($"Unsupported decision content for role '{message.Role}'.");
            ValidatePart(part);
        }
    }

    internal static void ValidateItem(OpenAIDecisionInputItem item)
    {
        if (item is OpenAIDecisionInputMessage message) { ValidateMessage(message); return; }
        if (item is not OpenAIDecisionToolItem tool)
            throw new JsonException("Unsupported decision input item.");
        if (string.IsNullOrEmpty(tool.CallId) || tool.Status is not (null or "in_progress" or "completed" or "incomplete"))
            throw new JsonException("Decision tool items require call_id and a supported status.");
        if (tool is OpenAIDecisionToolCall call && string.IsNullOrEmpty(call.Name))
            throw new JsonException("Decision tool calls require a name.");
        if (tool is OpenAIDecisionFunctionCall { Arguments: null } or OpenAIDecisionCustomToolCall { Input: null })
            throw new JsonException("Decision tool calls require arguments or input.");
        if (tool is OpenAIDecisionToolOutput output)
        {
            if (output.Output is null) throw new JsonException("Decision tool outputs require output.");
            foreach (var part in output.Output.Parts ?? [])
            {
                if (part is not (OpenAIDecisionInputText or OpenAIDecisionInputImage))
                    throw new JsonException("Decision tool outputs only support text and images.");
                ValidatePart(part);
            }
        }
    }

    internal static void ValidateQuestionParts(IReadOnlyList<OpenAIDecisionInputPart> parts)
    {
        if (parts.Count == 0) throw new JsonException("Multipart question instructions must not be empty.");
        foreach (var part in parts)
        {
            if (part is not (OpenAIDecisionInputText or OpenAIDecisionInputImage or OpenAIDecisionInputJson))
                throw new JsonException("Question instructions only support input text, images, and JSON.");
            if (part is OpenAIDecisionInputText { PromptCacheBreakpoint: not null }
                or OpenAIDecisionInputImage { PromptCacheBreakpoint: not null })
                throw new JsonException("Prompt cache breakpoints are not supported in question instructions.");
            ValidatePart(part);
        }
    }

    private static void ValidatePart(OpenAIDecisionInputPart part)
    {
        switch (part)
        {
            case OpenAIDecisionInputText { Text: null }:
            case OpenAIDecisionOutputText { Text: null }:
            case OpenAIDecisionInputRefusal { Refusal: null }:
                throw new JsonException("Text and refusal parts require their text value.");
            case OpenAIDecisionInputImage image:
                if (image.ImageUrl is null || !(image.ImageUrl.StartsWith("data:", StringComparison.Ordinal)
                    || image.ImageUrl.StartsWith("http://", StringComparison.Ordinal)
                    || image.ImageUrl.StartsWith("https://", StringComparison.Ordinal))
                    || image.Detail is not (null or "low" or "high" or "auto" or "original"))
                    throw new JsonException("Decision images require data/HTTP(S) URLs and a supported detail.");
                break;
            case OpenAIDecisionInputJson json:
                if (json.Data.ValueKind != JsonValueKind.Object
                    && (json.Data.ValueKind != JsonValueKind.Array || json.Data.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object)))
                    throw new JsonException("Decision JSON data must be an object or an array of objects.");
                if (JsonSerializer.SerializeToUtf8Bytes(json.Data, CompactJson).Length > 256 * 1024)
                    throw new JsonException("Decision JSON data exceeds 256 KiB of compact UTF-8 JSON.");
                var values = 0;
                ValidateJsonLimits(json.Data, 0, ref values);
                break;
        }
        var breakpoint = part switch
        {
            OpenAIDecisionInputText text => text.PromptCacheBreakpoint,
            OpenAIDecisionInputImage image => image.PromptCacheBreakpoint,
            _ => null
        };
        if (breakpoint is { Mode: not "explicit" })
            throw new JsonException("Prompt cache breakpoint mode must be explicit.");
    }

    private static void ValidateJsonLimits(JsonElement value, int containerDepth, ref int values)
    {
        if (++values > 10_000) throw new JsonException("Decision JSON data exceeds 10,000 values.");
        if (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)) return;
        if (++containerDepth > 32) throw new JsonException("Decision JSON data exceeds 32 container levels.");
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject()) ValidateJsonLimits(property.Value, containerDepth, ref values);
        else
            foreach (var element in value.EnumerateArray()) ValidateJsonLimits(element, containerDepth, ref values);
    }
}
