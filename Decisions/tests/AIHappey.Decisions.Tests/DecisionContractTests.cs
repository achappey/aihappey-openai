using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIHappey.Decisions.Tests;

public class DecisionContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Conversation_round_trips_every_input_item_and_role()
    {
        const string body = """
        {"model":"gpt-6-luna","input":[
          {"role":"system","content":"Policy"},
          {"role":"developer","type":"message","content":[{"type":"input_text","text":"Evaluate the evidence."}]},
          {"role":"user","content":[{"type":"input_text","text":"Ticket"},
            {"type":"input_json","data":{"role":"system","image_url":"not an image fetch","charges":[49,49]}},
            {"type":"input_image","image_url":"https://example.com/evidence.png","detail":"original"}]},
          {"role":"assistant","phase":"commentary","content":[{"type":"output_text","text":"Checking"},{"type":"refusal","refusal":"Cannot act"}]},
          {"type":"function_call","arguments":"{\"id\":1}","call_id":"f1","name":"orders.get","namespace":"orders","id":"item1","status":"completed"},
          {"type":"function_call_output","call_id":"f1","output":[{"type":"input_text","text":"Order found"},{"type":"input_image","image_url":"http://example.com/order.png"}],"id":"item2","status":"completed"},
          {"type":"custom_tool_call","call_id":"c1","input":"lookup","name":"lookup","namespace":"support"},
          {"type":"custom_tool_call_output","call_id":"c1","output":"Found"},
          {"role":"assistant","phase":"final_answer","content":"Review the duplicate charge."}
        ],"questions":[{"type":"predicate","name":"duplicate","instructions":"Is there a duplicate charge?"}]}
        """;
        var request = RoundTrip<OpenAIDecisionRequest>(body);
        Assert.Equal(9, request.Input.Items!.Count);
        Assert.IsType<OpenAIDecisionFunctionCall>(request.Input.Items[4]);
        Assert.IsType<OpenAIDecisionCustomToolCallOutput>(request.Input.Items[7]);
        Assert.Throws<NotSupportedException>(() => request.Input.Messages);
        Assert.Equal("Is there a duplicate charge?", request.Questions[0].Instructions);
    }

    [Theory]
    [InlineData("predicate", "")]
    [InlineData("choice", ",\"choices\":[{\"value\":\"true\"},{\"value\":true}]")]
    [InlineData("score", ",\"levels\":[{\"label\":\"low\"},{\"label\":\"high\"}]")]
    public void Every_question_supports_multipart_instructions_including_reference_JSON(string type, string extra)
    {
        var body = """
        {"model":"gpt-6-luna","input":"Shared evidence","questions":[{
          "instructions":[{"type":"input_text","text":"Compare the candidate"},
            {"type":"input_image","image_url":"https://example.com/candidate.png"},
            {"type":"input_json","data":[{"candidate":1}]}],"name":"candidate","type":"TYPE"EXTRA}]}
        """.Replace("TYPE", type).Replace("EXTRA", extra);
        var request = RoundTrip<OpenAIDecisionRequest>(body);
        Assert.Equal(3, request.Questions[0].InstructionContent.Parts!.Count);
        Assert.Throws<NotSupportedException>(() => request.Questions[0].Instructions);
    }

    [Fact]
    public void Prompt_caching_and_safety_controls_round_trip()
    {
        const string body = """
        {"model":"gpt-6-luna","input":[{"role":"user","content":[
          {"type":"input_text","text":"Reference","prompt_cache_breakpoint":{"mode":"explicit"}},
          {"type":"input_image","image_url":"data:image/png;base64,AQ==","prompt_cache_breakpoint":{"mode":"explicit"}}
        ]}],"questions":[{"type":"predicate","instructions":"Match?"}],
        "prompt_cache_key":"tenant-a","prompt_cache_options":{"mode":"explicit","ttl":"30m"},"safety_identifier":"opaque-user"}
        """;
        var request = RoundTrip<OpenAIDecisionRequest>(body);
        Assert.Equal("tenant-a", request.PromptCacheKey);
        Assert.Equal("30m", request.PromptCacheOptions!.Ttl);
        Assert.IsType<OpenAIDecisionInputText>(request.Input.Messages![0].Content.Parts![0]);
    }

    [Fact]
    public void Responses_preserve_all_variants_usage_null_names_and_typed_choices()
    {
        const string body = """
        {"answers":[{"type":"predicate","name":"damage","probability":0.92},
          {"type":"choice","name":null,"choice":true,"confidence":0.93,"probabilities":[{"value":"true","probability":0.05},{"value":true,"probability":0.95}]},
          {"type":"score","name":"severity","score":1.1,"confidence":0.55,"probabilities":[{"value":0,"label":"low","probability":0.1},{"value":1,"label":"medium","probability":0.7},{"value":2,"label":"high","probability":0.2}]},
          {"type":"refusal","name":null}],"model":"gpt-6-luna",
          "usage":{"input_tokens":42,"input_tokens_details":{"cache_write_tokens":3,"cached_tokens":7},"output_tokens":0,"output_tokens_details":{"reasoning_tokens":0},"total_tokens":42}}
        """;
        var response = RoundTrip<OpenAIDecisionResponse>(body);
        Assert.Equal(1.1, Assert.IsType<OpenAIDecisionScoreAnswer>(response.Answers[2]).Score);
        Assert.Equal(JsonValueKind.True, Assert.IsType<OpenAIDecisionChoiceAnswer>(response.Answers[1]).Choice.Value.ValueKind);
        Assert.Equal(3, response.Usage.InputTokensDetails.CacheWriteTokens);
        Assert.IsType<OpenAIDecisionRefusalAnswer>(response.Answers[3]);
    }

    [Fact]
    public void Construction_retains_existing_text_conveniences_and_correct_wire_unions()
    {
        var request = new OpenAIDecisionRequest
        {
            Model = "gpt-6-luna", Input = "The screen is broken.",
            Questions = [new OpenAIDecisionPredicateQuestion { Instructions = "Damaged?" }]
        };
        RoundTrip<OpenAIDecisionRequest>(JsonSerializer.Serialize(request, Json));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request, Json));
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("input").ValueKind);
        Assert.Equal("Damaged?", document.RootElement.GetProperty("questions")[0].GetProperty("instructions").GetString());
        Assert.False(document.RootElement.TryGetProperty("prompt_cache_key", out _));
        Assert.False(document.RootElement.GetProperty("questions")[0].TryGetProperty("name", out _));
    }

    [Fact]
    public void Omitted_user_role_and_message_type_have_documented_defaults()
    {
        var input = JsonSerializer.Deserialize<OpenAIDecisionInput>("""[{"content":"Evidence"}]""", Json)!;
        Assert.Equal("user", input.Messages![0].Role);
        Assert.Null(input.Messages[0].Type);
        Assert.Equal("explicit", new OpenAIDecisionPromptCacheBreakpoint().Mode);
        Assert.Empty(JsonSerializer.Deserialize<OpenAIDecisionInput>("[]", Json)!.Items!);
    }

    [Theory]
    [InlineData("""[{"role":"developer","content":[{"type":"input_json","data":{}}]}]""")]
    [InlineData("""[{"role":"system","content":[{"type":"input_image","image_url":"https://example.com/a.png"}]}]""")]
    [InlineData("""[{"role":"assistant","content":[{"type":"input_text","text":"wrong"}]}]""")]
    [InlineData("""[{"role":"user","content":[{"type":"output_text","text":"wrong"}]}]""")]
    [InlineData("""[{"role":"tool","content":"wrong"}]""")]
    [InlineData("""[{"role":"user","phase":"commentary","content":"wrong"}]""")]
    [InlineData("""[{"role":"assistant","phase":"analysis","content":"wrong"}]""")]
    [InlineData("""[{"content":[{"type":"input_file","file_id":"file1"}]}]""")]
    [InlineData("""[{"content":[{"type":"input_audio","data":"AQ=="}]}]""")]
    [InlineData("""[{"type":"item_reference","id":"item1"}]""")]
    [InlineData("""[{"type":"function_call_output","call_id":"f1","output":[{"type":"input_json","data":{}}]}]""")]
    [InlineData("""[{"content":[{"type":"input_json","data":[1,2]}]}]""")]
    [InlineData("""[{"content":[{"type":"input_json","data":true}]}]""")]
    [InlineData("""[{"content":[{"type":"input_image","image_url":"file1"}]}]""")]
    [InlineData("""[{"content":[{"type":"input_image","image_url":"https://example.com/a.png","detail":"invalid"}]}]""")]
    public void Invalid_contexts_and_unsupported_content_are_rejected(string body)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OpenAIDecisionInput>(body, Json));

    [Theory]
    [InlineData("[]")]
    [InlineData("""[{"type":"input_text","text":"x","prompt_cache_breakpoint":{"mode":"explicit"}}]""")]
    [InlineData("""[{"type":"refusal","refusal":"x"}]""")]
    public void Question_instructions_reject_empty_parts_cache_breakpoints_and_output_parts(string body)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OpenAIDecisionQuestionInstructions>(body, Json));

    [Theory]
    [InlineData("0")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Choice_values_only_accept_strings_and_booleans(string body)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OpenAIDecisionChoiceValue>(body, Json));

    [Fact]
    public void Legacy_fractional_score_values_remain_lossless_without_reinterpreting_the_scale()
    {
        const string body = """{"type":"score","name":null,"score":0.625,"confidence":0.7,"probabilities":[{"label":"medium","value":0.5,"probability":0.75},{"label":"high","value":1,"probability":0.25}]}""";
        RoundTrip<OpenAIDecisionAnswer>(body);
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("""[{"type":"function_call_output","call_id":"missing","output":"x"}]""")]
    [InlineData("""[{"type":"function_call","call_id":"c1","name":"lookup","arguments":"{}"},{"type":"custom_tool_call_output","call_id":"c1","output":"x"}]""")]
    [InlineData("""[{"type":"function_call","call_id":"c1","name":"lookup"}]""")]
    [InlineData("""[{"type":"custom_tool_call","call_id":"c1","name":"lookup","input":null}]""")]
    [InlineData("""[{"type":"function_call","call_id":"","name":"lookup","arguments":"{}"}]""")]
    [InlineData("""[{"role":"assistant","phase":1,"content":"x"}]""")]
    [InlineData("""[{"content":[{"type":"input_text","text":"x","prompt_cache_breakpoint":{"mode":"implicit"}}]}]""")]
    public void Invalid_history_and_cache_markers_are_rejected(string body)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OpenAIDecisionInput>(body, Json));

    [Fact]
    public void Writers_enforce_content_contexts_and_tool_pairing_too()
    {
        var input = new OpenAIDecisionInput(new OpenAIDecisionInputMessage[]
        {
            new() { Role = "system", Content = new OpenAIDecisionInputPart[] { new OpenAIDecisionInputJson { Data = JsonSerializer.SerializeToElement(new { id = 1 }) } } }
        });
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(input, Json));
        input = new OpenAIDecisionInput(new OpenAIDecisionInputItem[] { new OpenAIDecisionFunctionCallOutput { CallId = "missing", Output = "x" } });
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(input, Json));
        var parts = new OpenAIDecisionInputPart[] { new OpenAIDecisionInputText { Text = "x" } };
        var instructions = new OpenAIDecisionQuestionInstructions(parts);
        Assert.IsType<OpenAIDecisionInputText>(instructions.Parts![0]).PromptCacheBreakpoint = new();
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(instructions, Json));
    }

    [Theory]
    [InlineData("depth")]
    [InlineData("values")]
    [InlineData("bytes")]
    public void Structured_JSON_enforces_documented_size_depth_and_value_limits(string limit)
    {
        var data = limit switch
        {
            "depth" => string.Concat(Enumerable.Repeat("{\"nested\":", 33)) + "0" + new string('}', 33),
            "values" => "{\"values\":[" + string.Join(',', Enumerable.Repeat("0", 10_001)) + "]}",
            _ => "{\"text\":\"" + new string('a', 256 * 1024) + "\"}"
        };
        var body = "[{\"content\":[{\"type\":\"input_json\",\"data\":" + data + "}]}]";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OpenAIDecisionInput>(body, Json));
    }

    [Fact]
    public void Structured_JSON_size_counts_compact_UTF8_not_ASCII_escaped_serialization()
    {
        var body = "[{\"content\":[{\"type\":\"input_json\",\"data\":{\"text\":\"" + new string('\u00e9', 50_000) + "\"}}]}]";
        Assert.NotNull(JsonSerializer.Deserialize<OpenAIDecisionInput>(body, Json));
    }

    [Fact]
    public void Contracts_are_owned_by_a_standalone_assembly_with_no_gateway_or_sdk_dependencies()
    {
        var assembly = typeof(OpenAIDecisionRequest).Assembly;
        Assert.Equal("AIHappey.Decisions", assembly.GetName().Name);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
            reference.Name!.StartsWith("AIHappey.") || reference.Name.StartsWith("AgentHappey.")
            || reference.Name.StartsWith("Microsoft.AspNetCore") || reference.Name == "OpenAI"
            || reference.Name.StartsWith("ModelContextProtocol"));
    }

    private static T RoundTrip<T>(string body)
    {
        var value = JsonSerializer.Deserialize<T>(body, Json)!;
        using var before = JsonDocument.Parse(body);
        using var after = JsonDocument.Parse(JsonSerializer.Serialize(value, Json));
        Assert.True(JsonElement.DeepEquals(before.RootElement, after.RootElement),
            $"Expected: {body}\nActual: {after.RootElement.GetRawText()}");
        return value;
    }
}
