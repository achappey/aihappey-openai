using System.Text.Json;
using System.Text.Json.Serialization;
using AIHappey.Common.Model.Providers.OpenAI;
using AIHappey.Common.Model.Providers.OpenAI.Live;
using AIHappey.Core.Models;
using AIHappey.Core.Providers.OpenAI;
using Microsoft.AspNetCore.Http;

namespace AIHappey.OpenAI.Tests;

public class SerializationRegressionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Theory]
    [InlineData(typeof(AudioSpeechRequest), """{"model":"voice-model","input":"Hello","stream_format":"sse","future_option":{"enabled":true}}""")]
    [InlineData(typeof(OpenAIImageGenerationRequest), """{"model":"image-model","prompt":"A tree","stream":true,"partial_images":2,"future_option":7}""")]
    [InlineData(typeof(OpenAIImageEditRequest), """{"model":"image-model","prompt":"Edit","images":[{"image_url":"https://example.test/image.png"}],"mask":{"image_url":"data:image/png;base64,AQ=="},"future_option":false}""")]
    [InlineData(typeof(OpenAITranscriptionRequest), """{"model":"transcription-model","language":"en","response_format":"verbose_json","timestamp_granularities":["word","segment"],"chunking_strategy":{"type":"server_vad","prefix_padding_ms":100},"known_speaker_names":["Alice"],"known_speaker_references":["data:audio/wav;base64,AQ=="],"future_option":[1,2]}""")]
    public void Media_requests_preserve_existing_JSON_and_top_level_extension_data(Type type, string body)
        => RoundTrip(type, body);

    [Theory]
    [InlineData("\"Hello\"")]
    [InlineData("[\"Hello\",\"World\"]")]
    [InlineData("[1,2,3]")]
    [InlineData("[[1,2],[3,4]]")]
    public void Embedding_inputs_remain_unmodified_JSON_values(string input)
        => RoundTrip(typeof(OpenAIEmbeddingRequest), """{"model":"embedding-model","input":INPUT,"dimensions":2,"encoding_format":"base64","future_option":true}""".Replace("INPUT", input));

    [Theory]
    [InlineData("[0.5,-0.5]")]
    [InlineData("\"AACAPw==\"")]
    public void Embedding_outputs_keep_float_or_base64_shapes(string embedding)
        => RoundTrip(typeof(OpenAIEmbeddingResponse), """{"object":"list","model":"embedding-model","data":[{"object":"embedding","index":0,"embedding":VECTOR}],"usage":{"prompt_tokens":1,"total_tokens":1}}""".Replace("VECTOR", embedding));

    [Fact]
    public void Multipart_image_files_are_not_serialized_as_JSON()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var file = new FormFile(stream, 0, stream.Length, "image", "image.png");
        var edit = new OpenAIImageEditRequest { Model = "image-model", Prompt = "Edit", ImageFiles = [file], MaskFile = file };
        var json = JsonSerializer.SerializeToElement(edit, Json);
        Assert.Equal(2, json.EnumerateObject().Count());
        Assert.Equal("Edit", json.GetProperty("prompt").GetString());
        Assert.Equal("image-model", json.GetProperty("model").GetString());
        Assert.Same(file, new OpenAITranscriptionRequest { File = file, Model = "transcription-model" }.File);
    }

    [Fact]
    public void Transcription_multipart_file_and_unset_optional_fields_are_not_serialized_as_JSON()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var file = new FormFile(stream, 0, stream.Length, "file", "audio.wav");
        var request = new OpenAITranscriptionRequest { File = file, Model = "transcription-model" };
        var json = JsonSerializer.SerializeToElement(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Single(json.EnumerateObject());
        Assert.Equal("transcription-model", json.GetProperty("model").GetString());
        Assert.Same(file, request.File);
    }

    [Theory]
    [InlineData(typeof(AudioSpeechStreamDelta), """{"type":"speech.audio.delta","audio":"AQ=="}""")]
    [InlineData(typeof(AudioSpeechStreamDone), """{"type":"speech.audio.done","usage":{"input_tokens":1,"output_tokens":2,"total_tokens":3}}""")]
    [InlineData(typeof(OpenAIImageGenerationPartialImage), """{"type":"image_generation.partial_image","b64_json":"AQ==","created_at":123,"partial_image_index":0}""")]
    [InlineData(typeof(OpenAIImageGenerationCompleted), """{"type":"image_generation.completed","b64_json":"AQ==","created_at":123,"usage":{"input_tokens":1,"output_tokens":2,"total_tokens":3}}""")]
    [InlineData(typeof(OpenAIImageEditPartialImage), """{"type":"image_edit.partial_image","b64_json":"AQ==","created_at":123,"partial_image_index":1}""")]
    [InlineData(typeof(OpenAIImageEditCompleted), """{"type":"image_edit.completed","b64_json":"AQ==","created_at":123}""")]
    [InlineData(typeof(OpenAITranscriptionTextDelta), """{"type":"transcript.text.delta","delta":"Hello","segment_id":"s1","logprobs":[{"token":"Hello","bytes":[72,101],"logprob":-0.5}]}""")]
    [InlineData(typeof(OpenAITranscriptionTextDone), """{"type":"transcript.text.done","text":"Hello","usage":{"type":"duration","seconds":2}}""")]
    [InlineData(typeof(OpenAITranscriptionTextSegment), """{"type":"transcript.text.segment","id":"s1","start":0,"end":1,"text":"Hello","speaker":"Alice"}""")]
    public void Concrete_stream_events_keep_existing_discriminators_and_payloads(Type type, string body)
        => RoundTrip(type, body);

    [Theory]
    [InlineData("""{"type":"tokens","input_tokens":3,"output_tokens":2,"total_tokens":5,"input_token_details":{"audio_tokens":2,"text_tokens":1}}""", typeof(OpenAITranscriptionTokenUsage))]
    [InlineData("""{"type":"duration","seconds":2.5}""", typeof(OpenAITranscriptionDurationUsage))]
    public void Transcription_usage_keeps_polymorphic_serialization(string body, Type expectedType)
    {
        var usage = JsonSerializer.Deserialize<OpenAITranscriptionUsage>(body, Json)!;
        Assert.IsType(expectedType, usage);
        EqualJson(body, JsonSerializer.SerializeToElement(usage, Json));
    }

    [Fact]
    public void Verbose_transcription_keeps_existing_duration_only_usage_guard()
    {
        const string body = """{"task":"transcribe","language":"en","duration":2.5,"text":"Hello","words":[{"word":"Hello","start":0,"end":1}],"usage":{"type":"duration","seconds":2.5}}""";
        var response = (OpenAITranscriptionVerboseResponse)RoundTrip(typeof(OpenAITranscriptionVerboseResponse), body);
        Assert.Throws<ArgumentException>(() => response.Usage = new OpenAITranscriptionTokenUsage { InputTokens = 1, OutputTokens = 2, TotalTokens = 3 });
    }

    [Fact]
    public void Transcription_interface_preserves_logprob_byte_arrays_and_usage_fields()
    {
        const string body = """{"text":"Hello","logprobs":[{"token":"Hello","bytes":[72,101],"logprob":-0.5}],"usage":{"type":"tokens","input_tokens":1,"output_tokens":2,"total_tokens":3}}""";
        IOpenAITranscriptionResponse response = JsonSerializer.Deserialize<OpenAITranscriptionResponse>(body, Json)!;
        EqualJson(body, JsonSerializer.SerializeToElement(response, Json));
    }

    [Theory]
    [InlineData(typeof(ShellEnvironmentUnion), """{"container_id":"container-1","type":"container_reference"}""", typeof(ContainerReferenceEnvironment))]
    [InlineData(typeof(ContainerNetworkPolicyUnion), """{"type":"disabled"}""", typeof(ContainerNetworkPolicyDisabled))]
    [InlineData(typeof(ShellOutcomeUnion), """{"exit_code":0,"type":"exit"}""", typeof(ShellExitOutcome))]
    [InlineData(typeof(LiveRequestTransport), """{"sdp":"offer","type":"webrtc"}""", typeof(LiveWebRtcRequestTransport))]
    [InlineData(typeof(LiveResponseTransport), """{"type":"sip"}""", typeof(LiveSipResponseTransport))]
    [InlineData(typeof(LiveRequestTransport), """{"type":"livekit","future_option":true}""", typeof(LiveAdditionalRequestTransport))]
    [InlineData(typeof(LiveResponseTransport), """{"type":"livekit","url":"wss://example.test","future_option":7}""", typeof(LiveAdditionalResponseTransport))]
    [InlineData(typeof(LiveDecisionInstructions), """[{"text":"Check","type":"input_text"}]""", typeof(LiveDecisionInstructions))]
    [InlineData(typeof(LiveDecisionChoiceValue), "true", typeof(LiveDecisionChoiceValue))]
    [InlineData(typeof(LiveDecisionChoiceValue), "\"true\"", typeof(LiveDecisionChoiceValue))]
    [InlineData(typeof(LiveVoice), "\"alloy\"", typeof(LiveVoice))]
    [InlineData(typeof(LiveVoice), """{"id":"voice-1"}""", typeof(LiveVoice))]
    [InlineData(typeof(LiveEventPermissions<string>), "\"all\"", typeof(LiveEventPermissions<string>))]
    [InlineData(typeof(LiveEventPermissions<string>), "[]", typeof(LiveEventPermissions<string>))]
    public void Shell_and_Live_converters_keep_existing_union_shapes(Type type, string body, Type expectedType)
        => Assert.IsType(expectedType, RoundTrip(type, body));

    [Fact]
    public void Live_sessions_preserve_extension_data_without_a_new_wrapper()
        => RoundTrip(typeof(LiveSessionRequest), """{"session":{"model":"live-model","future_session_option":{"enabled":false},"audio":{"output":{"voice":"alloy"}}},"transport":{"type":"livekit","future_option":true}}""");

    [Fact]
    public void Realtime_client_secret_response_retains_the_original_fields()
        => RoundTrip(typeof(OpenAiRealtimeResponse), """{"value":"secret-fixture","expires_at":123,"session":{"type":"realtime","model":"live-model"}}""");

    [Fact]
    public void Model_list_and_price_converter_preserve_existing_numeric_and_string_input()
    {
        const string body = """{"object":"list","data":[{"id":"model-1","object":"model","created":123,"owned_by":"provider","name":"Model","type":"language","pricing":{"input":1.25,"output":2.5,"input_cache_read":0.25}}]}""";
        RoundTrip(typeof(ModelResponse), body);
        var price = JsonSerializer.Deserialize<ModelPricing>("""{"input":"1.25","output":"2.5"}""", Json)!;
        Assert.Equal(1.25m, price.Input);
        Assert.Equal(2.5m, price.Output);
        Assert.Equal(JsonValueKind.Number, JsonSerializer.SerializeToElement(price, Json).GetProperty("input").ValueKind);
    }

    private static object RoundTrip(Type type, string body)
    {
        var value = JsonSerializer.Deserialize(body, type, Json)!;
        EqualJson(body, JsonSerializer.SerializeToElement(value, type, Json));
        return value;
    }

    private static void EqualJson(string expected, JsonElement actual)
        => Assert.True(JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(expected), actual), actual.GetRawText());
}
