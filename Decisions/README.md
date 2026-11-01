# AIHappey Decisions

A small, standalone .NET class library for the OpenAI-compatible Decisions creation request and response wire contracts. The [library project](AIHappey.Decisions.csproj) follows the sibling Responses and Vercel project conventions and has **no external runtime dependencies**. It does not depend on either backend, an OpenAI SDK, ASP.NET, Responses, or Vercel.

This is a contracts library, not a client, provider runtime, endpoint implementation, or full server-side schema validator. It does not fetch images, run tools, implement caching, enforce model availability, or call an API. Organization usage reporting, voice integration, deployment, pricing, and data-retention policies are outside its scope.

## Contract coverage

- [Request, question, answer, typed-choice, and usage models](Models/DecisionModels.cs): all documented creation request/response fields, ordered answers, nullable answer names, predicate probabilities, choice distributions, score distributions, refusals, safety identifiers, and detailed cache/token usage.
- [Conversation and caching models](Models/DecisionConversation.cs): all four message roles, assistant phases, function/custom tool calls and outputs, structured JSON, string or multipart question instructions, cache keys/options, and explicit content breakpoints.
- [Core converters](Models/DecisionJsonConverters.cs) and [conversation converters](Models/DecisionConversationJsonConverters.cs): actual wire strings/arrays rather than wrapper objects, discriminator lookup independent of property order, role-specific content restrictions, and ordered tool-output pairing by matching earlier call ID and kind.
- Hosted HTTP(S) and inline data image URLs are representable, without downloading them. File IDs, files, audio, and item references are not supported.
- Structured JSON must be an object or an array of objects. JSON keys inside evidence remain ordinary data, not messages or image-fetch instructions. The converters check the documented 32-container, 10,000-value, and 256-KiB compact UTF-8 limits.
- Multipart instructions must be nonempty and cannot contain cache breakpoints. The supplied formal API reference includes structured JSON in instructions, although the supplied prose guide mentions only text and images; this library follows the formal reference.

The models intentionally retain string-valued settings and permissive numeric storage, like the existing sibling contracts. API/service validation still owns the remaining string/count limits, choice uniqueness, model availability, valid cache modes/TTL, and numerical answer semantics. Optional null fields may be omitted on serialization. Unknown top-level request/response fields are preserved as extension data; unknown discriminated variants are rejected.

## Construction and migration

All public types retain the [existing Decisions namespace](Models/DecisionModels.cs:4) and assembly identity. The gateway and agents reference the [same library project](AIHappey.Decisions.csproj), now located inside the OpenAI contracts repository; the gateway does not define the contracts and agents do not compile source files from the gateway repository. This project remains independent of the [media/protocol contract project](../AIHappey.OpenAI.csproj), Responses, and Vercel. No NuGet publication or Git repository initialization is performed by this relocation.

The familiar type names and text construction remain available. For new integrations:

- Use the ordered items collection on the [input contract](Models/DecisionModels.cs) for mixed messages and tool history. The legacy message-only accessor throws if tool items are present instead of silently dropping them.
- Use the typed instruction-content property on the [question contract](Models/DecisionModels.cs) for either text or multipart instructions. The legacy text-only instructions property remains a convenience accessor; reading it for multipart instructions throws instead of flattening the parts.
- Assistant output/refusal parts and JSON evidence have dedicated [content types](Models/DecisionConversation.cs). System/developer messages accept input text only; tool outputs accept input text and images only.
- Cache controls and breakpoints are represented by the [caching models](Models/DecisionConversation.cs), independently of whether an execution backend supports them.

## Runtime compatibility report

**Complete wire-field coverage does not mean complete runtime API support.** This extraction deliberately does not expand execution behavior in either backend.

| Capability in the supplied reference | Current backend behavior after migration |
| --- | --- |
| Developer/system/assistant history and function/custom tool history | The existing OpenAI-compatible execution paths still accept user-message evidence only. Unsupported history fails rather than being discarded. |
| JSON evidence and multipart question instructions | Representable by this library, but rejected by the existing native gateway/agent execution adapters. Vercel's existing structured-state behavior is unchanged. |
| Public HTTP(S) image URLs | Representable by this library; existing native gateway validation and agents still require inline image data. The existing request-wide 128-image runtime restriction remains. Provider-specific image restrictions also remain. |
| Prompt caching | Top-level controls continue to pass through the generic gateway transport, which already forwarded arbitrary top-level extension options. Adapters that map requests and agents reject unsupported typed cache controls. Shared-input cache breakpoints are not added to runtime execution. |
| 1–400 questions, 2–255 choices, 2–10 score levels | Existing execution validators retain their previous checks and provider-specific limits; they are not upgraded to enforce the supplied GA limits in this change. |
| Zero-based integer score-level indices and weighted index scores | **Known incompatibility:** agents and some compatible providers use normalized fractional scales. Existing output is unchanged. The score-probability value remains permissive numerical storage to preserve those payloads, not a claim that fractional indices conform to OpenAI's reference. |
| Choice/score confidence | Agents retain their existing maximum-option-probability calculation. Do not assume this reproduces the native model's separately returned confidence field. |
| Supported model | The library does not hard-code the guide's single model. Gateway provider IDs and agent catalog IDs retain their existing meaning. |
| Usage | All documented usage fields are representable. Actual values still come from each runtime/provider; no billing behavior changes are introduced. |

The supplied guide recommends unique question names; the formal reference makes names optional and returns answers in order. Existing unnamed/repeated-name handling remains unchanged. Likewise, server limits shown on the response schema are not substituted for request limits.

Coverage was checked against the specification supplied with this change, not an independently retrieved live OpenAI schema. No live API, latency, pricing, compliance, or model-availability claims are verified by the tests.

## Validation

The [standalone test project](tests/AIHappey.Decisions.Tests/AIHappey.Decisions.Tests.csproj) references only this library. Run it with the .NET test runner. Its [contract tests](tests/AIHappey.Decisions.Tests/DecisionContractTests.cs) cover construction, all conversation/answer variants, multipart instructions, caching, JSON limits, nullable names, context rejection, legacy score preservation, and assembly dependency isolation. It does not require API credentials or network access to a model.

The backend repositories also contain migration regressions for shared assembly ownership, unsupported-input rejection, unchanged agent score semantics, and existing generic-transport option passthrough.
