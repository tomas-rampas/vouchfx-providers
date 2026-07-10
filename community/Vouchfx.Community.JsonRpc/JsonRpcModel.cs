// Vouchfx.Community.JsonRpc — rpc.json-rpc step model.
//
// Strongly-typed records only (no Dictionary<string,object> — engine hard invariant,
// CLAUDE.md §13 / blueprint §13). Three records:
//   JsonRpcResultAssertion — one {path, value} JSONPath assertion evaluated against
//                            the response envelope's $.result.
//   JsonRpcExpect          — the optional expect block: EITHER `result` (a list of
//                            assertions) OR `error.code` (a negative test), never both.
//   JsonRpcModel            — the top-level model: url, method, params, notification,
//                            and the optional expect block.
//
// YAML shape (see the provider README for worked examples):
//   type: rpc.json-rpc
//   url: "http://{host}:{port}/rpc"
//   method: sum
//   params:
//     a: 2
//     b: 3
//   expect:
//     result:
//       - path: "$.sum"
//         value: 5
//   capture:
//     sum: "$.result.sum"
//   verifyMode: RETRY
//   timeout: PT3S
using Vouchfx.Sdk;

namespace Vouchfx.Community.JsonRpc;

/// <summary>
/// A single JSONPath assertion evaluated against the JSON-RPC response envelope's
/// <c>result</c> value.
/// </summary>
/// <param name="Path">
/// A JSONPath expression (JsonPath.Net syntax) evaluated against the response
/// envelope's <c>result</c> node, e.g. <c>"$.sum"</c> or <c>"$"</c> for a scalar
/// result.  Never <see langword="null"/>.
/// </param>
/// <param name="ExpectedJson">
/// The expected value, captured at bind time as a JSON literal (e.g. <c>"5"</c>,
/// <c>"\"ok\""</c>, <c>"true"</c>) so the runtime comparison is a structural JSON
/// equality (via <c>JsonNode.DeepEquals</c>), not a string comparison.  Never
/// <see langword="null"/>.
/// </param>
public sealed record JsonRpcResultAssertion(string Path, string ExpectedJson);

/// <summary>
/// The optional <c>expect</c> block for an <c>rpc.json-rpc</c> step.  Exactly one of
/// <see cref="Result"/> or <see cref="ErrorCode"/> may be declared (enforced by
/// <see cref="JsonRpcProvider.Validate"/>); both may be absent (a "bare call" — see
/// the provider's Emit remarks for the resulting default verdict rule).
/// </summary>
/// <param name="Result">
/// Zero or more JSONPath assertions evaluated against the response envelope's
/// <c>result</c>.  Mutually exclusive with <see cref="ErrorCode"/>.
/// <see langword="null"/> when the step declares no <c>expect.result</c> block.
/// </param>
/// <param name="ErrorCode">
/// The JSON-RPC error code the response's <c>error.code</c> must equal — a negative
/// test ("this call is expected to fail with exactly this code").  Mutually
/// exclusive with <see cref="Result"/>.  <see langword="null"/> when the step
/// declares no <c>expect.error</c> block.
/// </param>
public sealed record JsonRpcExpect(
    IReadOnlyList<JsonRpcResultAssertion>? Result = null,
    int? ErrorCode = null);

/// <summary>
/// Strongly-typed model for the <c>rpc.json-rpc</c> step kind (JSON-RPC 2.0 over HTTP).
/// </summary>
/// <param name="Url">
/// The absolute target URL.  May contain <c>{placeholder}</c> tokens (resolved
/// against <c>Vars</c>) and <c>${secret:source/path}</c> references (resolved via the
/// engine's secret subsystem), both at step-execution time — never at bind or compile
/// time (§17).  Never <see langword="null"/>.
/// </param>
/// <param name="Method">
/// The JSON-RPC <c>method</c> name.  Never <see langword="null"/>.
/// </param>
/// <param name="ParamsJson">
/// The JSON-RPC <c>params</c> value, pre-serialised at bind time to a JSON object
/// (from a YAML mapping) or JSON array (from a YAML sequence) literal. Per JSON-RPC
/// 2.0 §4.2, <c>params</c> MUST be structured when present — <see cref="JsonRpcProvider.Validate"/>
/// rejects a scalar. Every STRING LEAF in this JSON tree may contain a
/// <c>{placeholder}</c> token or a <c>${secret:source/path}</c> reference, both
/// resolved at step-execution time, exactly like <see cref="Url"/>.
/// <see langword="null"/> when the step declares no <c>params</c> field, in which case
/// the emitted request envelope omits <c>params</c> entirely.
/// </param>
/// <param name="Notification">
/// <see langword="true"/> when this is a JSON-RPC <em>notification</em> (DSL: the
/// author's <c>notification: true</c>) — the emitted request omits <c>id</c> entirely
/// and the step asserts only transport-level success (a 2xx HTTP status); the response
/// body is never parsed as a JSON-RPC envelope.  Defaults to <see langword="false"/>.
/// </param>
/// <param name="Expect">
/// The optional expectation block.  <see langword="null"/> for a "bare call" (see
/// <see cref="JsonRpcProvider.Emit"/> for the resulting default verdict rule).
/// </param>
public sealed record JsonRpcModel(
    string Url,
    string Method,
    string? ParamsJson,
    bool Notification,
    JsonRpcExpect? Expect) : IStepModel;
