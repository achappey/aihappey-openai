# AIHappey OpenAI contracts

Architectural consolidation of existing OpenAI-compatible contracts. There are two independent libraries in this repository:

- [OpenAI contracts project](AIHappey.OpenAI.csproj): media, embeddings, model-list, realtime client-secret, Live, tool, and OpenAI metadata contracts and JSON converters extracted from the gateway's Core/Common projects.
- [Decisions project](Decisions/AIHappey.Decisions.csproj): the existing Decisions library, relocated with its assembly identity, public namespaces, models, converters, and [regression tests](Decisions/tests/AIHappey.Decisions.Tests/DecisionContractTests.cs) preserved. See the [Decisions documentation](Decisions/README.md).

Neither library references the other. Neither references a backend, Unified, a provider SDK, Responses, ChatCompletions, Messages, Interactions, or Vercel. The [solution](AIHappey.OpenAI.slnx) includes both libraries and both standalone test projects.

## Relocation inventory

| Previous owner | Contracts now owned here |
| --- | --- |
| Gateway Core models | [Speech](Models/AudioSpeechRequest.cs), [embeddings](Models/EmbeddingModels.cs), [images and image streams](Models/OpenAIImageModels.cs), [transcription requests](Models/Transcriptions/OpenAIRequestModels.cs), [transcription responses](Models/Transcriptions/OpenAIResponseModels.cs), and [transcription streams](Models/Transcriptions/OpenAIResponseModels.Streaming.cs). |
| Gateway Core model-list contracts | [List response](Models/ModelResponse.cs), [model](Models/Model.cs), [pricing DTO](Models/ModelPricing.cs), and its [JSON converter](Models/TokenPriceConverter.cs). Catalog loading, pricing calculations, storage, and resolver configuration remain in the gateway. |
| Gateway Common OpenAI model directory | OpenAI metadata, [Shell contracts and union converters](Model/Providers/OpenAI/Shell.cs), and [Live models](Model/Providers/OpenAI/Live/LiveSessionModels.cs) with their [JSON converters](Model/Providers/OpenAI/Live/LiveJsonConverters.cs) and existing contract validation. |
| Gateway OpenAI realtime provider file | Only the [client-secret response DTO](Models/Realtime/OpenAiRealtimeResponse.cs); the provider implementation remains in the gateway. |
| Former Decisions repository | The [Decisions project](Decisions/AIHappey.Decisions.csproj), four model/converter files, documentation, and standalone tests. |

Whole contract source files were moved without editing their contents. The embedded realtime response class was extracted unchanged, including its original namespace. This is not an API compatibility audit: existing types, defaults, attributes, JSON shapes, converter behavior, validation, and known limitations are retained.

## Dependencies and consumer migration

Existing public namespaces are deliberately unchanged; rebuilding existing source consumers does not require mass import or provider edits. Gateway Core/Common reference the OpenAI library, and gateway/agents reference the relocated Decisions project. Local development requires these sibling repository directories, as with the other extracted contracts.

Contracts extracted from Core/Common now belong to the OpenAI contract assembly. Precompiled consumers must be rebuilt; reflection or persisted assembly-qualified type names may require updating. No binary forwarding facade or duplicate models are introduced. Decisions retains its original assembly identity and does not acquire media or ASP.NET dependencies.

The OpenAI project retains an ASP.NET framework reference because existing transcription/image-edit requests expose multipart form-file properties. Replacing those properties would change the public contract and is outside this relocation. The library has no provider SDK or external NuGet runtime dependencies. It targets the same .NET version as its source projects.

## Explicit exclusions

- Providers, HTTP transports, endpoints, runtime validation/adapters, Unified mappings, model resolution, storage, and pricing logic stay in their owning backends.
- Generic realtime envelopes and Vercel media/video contracts stay in their existing libraries. Translation and video paths currently use provider-local payloads, Vercel contracts, or SDK-owned models; no missing OpenAI wire contracts are invented here.
- Responses and ChatCompletions remain independent and unchanged. Live's existing response/delegation-shaped models are moved as-is, not replaced with models from those libraries.
- No compatibility expansion, package publication, or Git repository initialization is part of this consolidation.

## Validation

Run the .NET test runner against the [solution](AIHappey.OpenAI.slnx). The [standalone OpenAI test project](tests/AIHappey.OpenAI.Tests/AIHappey.OpenAI.Tests.csproj) references only the OpenAI contract library. Tests cover assembly ownership/dependency direction, converter co-location, multipart property identity, and representative existing serialization behavior. The relocated Decisions test project retains its existing contract regressions. Gateway and agent tests additionally exercise unchanged runtime adapters and shared Decisions assembly ownership.
