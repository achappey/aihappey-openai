using System.Reflection;
using System.Text.Json.Serialization;
using AIHappey.Common.Model.Providers.OpenAI;
using AIHappey.Common.Model.Providers.OpenAI.Live;
using AIHappey.Core.Models;
using AIHappey.Core.Providers.OpenAI;
using Microsoft.AspNetCore.Http;

namespace AIHappey.OpenAI.Tests;

public class DependencyBoundaryTests
{
    [Theory]
    [InlineData(typeof(AudioSpeechRequest), "AIHappey.Core.Models")]
    [InlineData(typeof(IAudioSpeechStreamEvent), "AIHappey.Core.Models")]
    [InlineData(typeof(OpenAITranscriptionRequest), "AIHappey.Core.Models")]
    [InlineData(typeof(IOpenAITranscriptionResponse), "AIHappey.Core.Models")]
    [InlineData(typeof(IOpenAITranscriptionStreamEvent), "AIHappey.Core.Models")]
    [InlineData(typeof(OpenAIImageEditRequest), "AIHappey.Core.Models")]
    [InlineData(typeof(IOpenAIImageStreamEvent), "AIHappey.Core.Models")]
    [InlineData(typeof(OpenAIEmbeddingRequest), "AIHappey.Core.Models")]
    [InlineData(typeof(ModelResponse), "AIHappey.Core.Models")]
    [InlineData(typeof(OpenAiRealtimeResponse), "AIHappey.Core.Providers.OpenAI")]
    [InlineData(typeof(OpenAiProviderMetadata), "AIHappey.Common.Model.Providers.OpenAI")]
    [InlineData(typeof(OpenAiRealtimeProviderMetadata), "AIHappey.Common.Model.Providers.OpenAI")]
    [InlineData(typeof(ShellEnvironmentUnion), "AIHappey.Common.Model.Providers.OpenAI")]
    [InlineData(typeof(LiveSessionRequest), "AIHappey.Common.Model.Providers.OpenAI.Live")]
    public void Relocated_types_have_one_contract_assembly_and_keep_their_namespaces(Type type, string originalNamespace)
    {
        Assert.Equal("AIHappey.OpenAI", type.Assembly.GetName().Name);
        Assert.Equal(originalNamespace, type.Namespace);
        Assert.Same(typeof(AudioSpeechRequest).Assembly, type.Assembly);
    }

    [Fact]
    public void Contract_assembly_references_only_platform_assemblies()
    {
        Assert.All(typeof(AudioSpeechRequest).Assembly.GetReferencedAssemblies(), reference =>
            Assert.True(reference.Name!.StartsWith("System.", StringComparison.Ordinal)
                || reference.Name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal), reference.FullName));
    }

    [Fact]
    public void All_attributed_contract_converters_are_owned_by_the_same_assembly()
    {
        var assembly = typeof(AudioSpeechRequest).Assembly;
        var converters = assembly.GetExportedTypes().SelectMany(type =>
            type.GetCustomAttributes<JsonConverterAttribute>()
                .Concat(type.GetProperties().SelectMany(property => property.GetCustomAttributes<JsonConverterAttribute>())));
        Assert.NotEmpty(converters);
        Assert.All(converters, attribute => Assert.Same(assembly, attribute.ConverterType!.Assembly));
        Assert.Same(assembly, typeof(TokenPriceConverter).Assembly);
        Assert.Same(assembly, typeof(ShellEnvironmentUnionConverter).Assembly);
        Assert.Same(assembly, typeof(LiveEventPermissionsJsonConverterFactory).Assembly);
    }

    [Fact]
    public void Multipart_properties_retain_the_existing_ASPNET_types()
    {
        Assert.Equal(typeof(IFormFile), typeof(OpenAITranscriptionRequest).GetProperty(nameof(OpenAITranscriptionRequest.File))!.PropertyType);
        Assert.Equal(typeof(IFormFile[]), typeof(OpenAIImageEditRequest).GetProperty(nameof(OpenAIImageEditRequest.ImageFiles))!.PropertyType);
        Assert.Equal(typeof(IFormFile), typeof(OpenAIImageEditRequest).GetProperty(nameof(OpenAIImageEditRequest.MaskFile))!.PropertyType);
    }
}
