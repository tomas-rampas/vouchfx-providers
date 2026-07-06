# `rpc.json-rpc` — JSON-RPC 2.0 over HTTP

A worked **sample** provider for the vouchfx community hub — richer than the
[copyable `template/`](../../template/) skeleton, but not (yet) a `verified/` submission.
It exists to demonstrate a *real* HTTP-calling, BCL-plus-JsonPath.Net provider outside
the engine repo, following exactly the same patterns the engine's own Core `http.rest`
and `mail-expect.smtp` providers use.

## What it is

[JSON-RPC 2.0](https://www.jsonrpc.org/specification) is a small, transport-agnostic
remote-procedure-call wire protocol: a JSON request envelope naming a `method` and
`params`, and a JSON response envelope carrying either a `result` or an `error`. It is
the wire protocol behind:

- the **Language Server Protocol** (every editor ↔ language-server exchange),
- **Ethereum** JSON-RPC APIs (`eth_call`, `eth_getBalance`, …),
- **Bitcoin Core**'s RPC interface,
- and many internal microservice RPC layers.

This provider issues JSON-RPC 2.0 requests over HTTP and asserts on the response —
letting an `.e2e.yaml` suite exercise a JSON-RPC endpoint the same way `http.rest`
exercises a plain REST one.

## Worked examples

### 1. Happy path, with a capture

```yaml
metadata:
  name: jsonrpc-sum-happy-path

environment:
  services:
    calc:
      image: my-org/jsonrpc-calculator:latest

steps:
  - id: call-sum
    type: rpc.json-rpc
    url: "http://{calc-host}:{calc-port}/rpc"
    method: sum
    params:
      a: 2
      b: 3
    expect:
      result:
        - path: "$.sum"
          value: 5
    capture:
      total: "$.result.sum"
```

`params` is a YAML **mapping**, so it becomes a JSON-RPC named-params **object**
(`{"a":2,"b":3}`). `expect.result` runs a JSONPath assertion against the response
envelope's `result` value. `capture` (the engine-standard field, DSL §6.1) reads the
**full** envelope — hence `"$.result.sum"`, not `"$.sum"` — and writes `total` into
`Vars` for a later step.

`url`, `method`, and every **string leaf** of `params` are resolved at
step-execution time through the same `{placeholder}` + `${secret:source/path}`
mechanism (`Secret_Helpers.ResolveTemplate`, §17) — see examples 3 and 4 below,
where `params` carries a `{orderId}` placeholder. `params` is parsed into a JSON
tree first and each string value is resolved individually, never by
template-substituting the raw JSON text before parsing, so a resolved value can
never corrupt the surrounding JSON structure.

### 2. Negative test — asserting a specific JSON-RPC error

```yaml
steps:
  - id: call-unknown-method
    type: rpc.json-rpc
    url: "http://{calc-host}:{calc-port}/rpc"
    method: subtractWithCarry   # deliberately unsupported by the target service
    expect:
      error:
        code: -32601            # JSON-RPC 2.0 standard "Method not found"
```

`expect.error.code` is a negative test: the step **passes** only when the response
genuinely is a JSON-RPC error envelope with exactly this code. `expect.result` and
`expect.error` are mutually exclusive — declaring both fails model validation before
any request is sent.

### 3. RETRY — polling an eventually-consistent read

```yaml
steps:
  - id: wait-for-projection
    type: rpc.json-rpc
    url: "http://{projector-host}:{projector-port}/rpc"
    method: getOrderStatus
    params:
      orderId: "{orderId}"
    expect:
      result:
        - path: "$.status"
          value: "SHIPPED"
    verifyMode: RETRY
    timeout: PT10S
```

`verifyMode: RETRY` is a purely **engine-side** wrapper (`CsxAssembler` /
`Platform.Engine.Abstractions.Retry.RetryRunner`) that re-invokes this provider's
emitted block, unchanged, until it passes or `timeout` elapses. The provider does not
implement (and does not need) any "RETRY capability" interface — the only contract it
honours is: write exactly one `StepOutcome` on every invocation, and use `Fail` (never
`Inconclusive`) for a not-yet-satisfied assertion. The engine converts a sustained
`Fail` into `Inconclusive` once the window elapses; see the table below.

The `{orderId}` inside `params.orderId` above is a genuine substitution, not a
literal: it is resolved from `Vars` on every poll, exactly like `{projector-host}`
in `url`.

### 4. Fire-and-forget notification

```yaml
steps:
  - id: notify-audit-log
    type: rpc.json-rpc
    url: "http://{audit-host}:{audit-port}/rpc"
    method: recordEvent
    params: ["order.shipped", "{orderId}"]
    notification: true
```

`params` is a YAML **sequence** here, so it becomes a JSON-RPC **positional** params
array (`["order.shipped", "<orderId>"]`, with `{orderId}` resolved from `Vars` the
same way as the mapping form above). `notification: true` omits the request `id`
entirely; the step asserts only that the transport call succeeded (a 2xx HTTP status)
and never parses a response envelope — there usually isn't one. `notification` is
incompatible with `expect` (rejected at model validation) and with `capture` (there is
no response body to capture from — see "Known limitations").

## Verdict-mapping table

This is the teaching core of the sample — verified against the engine's own exception-
to-verdict conventions (`http.rest`, `mail-expect.smtp`) rather than invented from
scratch.

| Condition | Verdict | Notes |
|---|---|---|
| Connection refused / DNS failure / TLS failure | `EnvironmentError` | Generic catch-all; a run-environment problem, not a product defect (§12.1). |
| Response body is not valid JSON | `EnvironmentError` | The server returned something that isn't even a JSON-RPC envelope. |
| Resolved `url` scheme is not `http`/`https` | `EnvironmentError` | A defence-in-depth guard, thrown before any request is sent. |
| `${secret:source/path}` resolves to nothing / unknown source | `EnvironmentError` | Reference-only observation (§17) — never the value, never even the exception message. |
| Client-side timeout (`TaskCanceledException` / `TimeoutException`) | `Inconclusive` | Mirrors `http.rest`: "the test could not complete due to a stall," not a defect. |
| `expect.result` declared, response is a JSON-RPC `error` instead | `Fail` | `{"unexpectedError":true}` |
| `expect.result` declared, response `id` does not match the request | `Fail` | `{"idMismatch":true}` |
| `expect.result` declared, any JSONPath assertion mismatches | `Fail` | `{"resultMismatch":{"path":...,"expected":...,"actual":...}}` — renderable as a diff, see below. |
| `expect.result` declared, all assertions match | `Pass` | |
| `expect.error.code` declared, response is a `result` envelope instead | `Fail` | `{"unexpectedResult":true}` |
| `expect.error.code` declared, response has neither `result` nor `error` | `Fail` | `{"missingError":true}` |
| `expect.error.code` declared, error present but code differs | `Fail` | `{"errorCodeMismatch":{"expected":...,"actual":...}}` — renderable as a diff. |
| `expect.error.code` declared, code matches | `Pass` | |
| Neither `expect.result` nor `expect.error` declared ("bare" call), an `error` envelope arrives | `Fail` | `{"unexpectedError":true}` |
| Bare call, `id` mismatch | `Fail` | `{"idMismatch":true}` |
| Bare call, clean success envelope with matching `id` | `Pass` | |
| `notification: true`, HTTP status is 2xx | `Pass` | No envelope is parsed. |
| `notification: true`, HTTP status is not 2xx | `Fail` | |
| `capture:` declared, JSONPath yields no match against the full envelope | `Inconclusive` | `{"captureUnmet":"<varName>"}` — mirrors `http.rest`'s "upstream-capture-unmet" convention exactly; only evaluated when the primary assertion above did not already resolve to `Fail`. |
| `notification: true` **and** `capture:` both declared | `Fail` | Rejected at compile (`Emit`) time, not validation time — see "Known limitations". An author misconfiguration (a step shape that can never do what it declares), not a timing uncertainty, so it deliberately does **not** use `Inconclusive` (which would not break CI by default and could hide the mistake). |
| `expect.result` declared as an empty list (`result: []`) | *(rejected)* | Model-validation error, not a runtime verdict — an empty list would otherwise silently degrade to bare-call semantics; see "Known limitations". |
| `params` is present but not a mapping or a sequence (a scalar) | *(rejected)* | Schema **and** model-validation error — JSON-RPC 2.0 §4.2 requires `params` to be structured when present. |
| `verifyMode: RETRY`, the assertion never converges before `timeout` | `Inconclusive` | Written entirely by the **engine's** `RetryRunner`, not by this provider — a sustained `Fail` (or `Inconclusive`) is converted once the polling window elapses. |

Two of the `Fail` shapes above — `resultMismatch` and `errorCodeMismatch` — implement
the optional `IStepDiffRenderer` interface, so a failing assertion renders as a small
expected-vs-observed table in supporting renderers, exactly as the Core
`mail-expect.smtp` provider does for its own count mismatches.

## Known limitations

- **HTTP transport only.** No WebSocket, no stdio (the LSP transport most editors
  actually use), no raw TCP framing. Adding a transport is a separate provider (or a
  richer model with a `transport:` discriminator) — out of scope for this sample.
- **No batch requests.** The JSON-RPC 2.0 spec allows a request array processed as a
  batch; this provider always sends exactly one request object per step.
- **No `headers` field.** Unlike `http.rest`, this sample's model has no way to set
  arbitrary request headers (e.g. `Authorization`). The engine's canonical pattern for
  a header *value* is `Secret_Helpers.ResolveTemplate` inside the emitted helper's
  guarded region (exactly as this provider already does for `url`, `method`, and every
  string leaf of `params`) — adding `headers` would cost only a small, mechanical
  amount of extra Bind/Emit plumbing (parallel name/value-template arrays, mirroring
  `http.rest`'s own header handling almost verbatim). It is omitted here to keep the
  teaching surface focused on the JSON-RPC envelope semantics themselves.
- **`url`, `method`, and `params` all support `${secret:...}`**, even though the brief
  for this sample only asked for `{placeholder}` support — `Secret_Helpers.ResolveTemplate`
  gives both in a single call, at no extra cost, so it was included throughout. `params`
  is parsed into a JSON tree and each STRING LEAF is resolved individually (never by
  template-substituting the raw JSON text before parsing), so a resolved value can never
  corrupt the surrounding JSON structure, even if it contains a quote or a brace.
- **Captures are always evaluated as JSONPath**, regardless of an explicit
  `{xpath: ...}` capture form — a JSON-RPC envelope is never XML, so an XPath-typed
  capture is simply passed through as raw expression text (it will normally just fail
  to match, yielding `Inconclusive`, not a hard validation error).
- **`notification: true` + `expect`** is rejected at **model validation** (a clear,
  author-facing error). **`notification: true` + `capture`** cannot be rejected there —
  `IProjectContext` (available in `Validate`) carries no view of the step's `capture`
  map; only `ICompileContext` (available in `Emit`) does. That combination is instead
  short-circuited at `Emit` time to a trivial `Fail` block that skips the HTTP call
  entirely, with a `{"error": "..."}` observation explaining why. `Fail`, not
  `Inconclusive`: this is an author misconfiguration, not a timing uncertainty, and
  `Inconclusive` does not break CI by default (§12.1) — using it here would let the
  mistake hide.
- **`expect.result` must be non-empty when declared.** Writing `expect.result: []`
  is rejected at model validation rather than silently degrading to bare-call
  semantics — an author who writes an empty list almost certainly means "assert a
  result exists," which is a stronger claim than a bare call actually checks.
- **`params` must be structured.** Per JSON-RPC 2.0 §4.2, `params` must be a mapping
  or a sequence when present; a bare scalar (`params: 5`, `params: "x"`) is rejected
  both by the JSON Schema and by `Validate`.
- **Response `id` matching is string-only.** This provider always sends the request
  `id` as a JSON *string* (the step id) and matches the response `id` with an ordinal
  string comparison. Some JSON-RPC ecosystems — Ethereum and Bitcoin Core's RPC APIs
  among them — conventionally use integer ids, and a server that coerces or echoes a
  numeric id back will fail the id-match check even though the call itself succeeded.
  There is no per-step way to opt into numeric ids in this sample.
- **No `IResourceContributor`.** This provider declares no Aspire-managed
  infrastructure — the target is whatever absolute URL the author supplies, resolved
  entirely at execution time. There is consequently no dependency-reconciliation check
  against `environment.services`/`environment.dependencies` the way `http.rest` and
  `mail-expect.smtp` have.
- **This sample needs a `JsonPath.Net` `PackageReference`** (pinned to `3.0.2`, matching
  the engine's own `Directory.Packages.props` entry) beyond `Platform.Sdk` — verified,
  not assumed: the engine's `capture:` field (and this provider's own `expect.result`)
  is evaluated with JSONPath.Net **inside the provider's own emitted CSX**, exactly as
  the Core `http.rest` and `mq-expect.*` providers do, which requires both the compile-time
  package reference (so `ICompileReferenceContributor` can name the type) and the
  contributor interface itself.
- **Conformance tests use a small custom harness (`JsonRpcHarness`), not only the
  published `Platform.Sdk.Testing.ProviderTestHarness`.** `ProviderTestHarness` does not
  run the `ICompileReferenceContributor` stage, and its default Roslyn reference set
  lacks `System.Net.Http` / `System.Text.Json` / `JsonPath.Net` entirely — a genuine
  HTTP-calling provider's execution tests cannot compile through it as-is. See
  `Community.Steps.JsonRpc.Tests/JsonRpcHarness.cs` for the full explanation and the
  engine-repo precedent (`HttpRestExecutionTests.cs`) this mirrors. Tests that halt
  *before* any compile (schema/model-validation rejection) use the published harness
  directly, with no workaround needed.

## Mapping to the community-hub path

This sample lives under `samples/`, alongside `template/` and `verified/`. It is not
itself submitted through either governance path in [`CONTRIBUTING.md`](../../CONTRIBUTING.md) —
it is reference material, built and CI-tested in this repository the same way the
engine's own worked examples (`examples/Example.Steps.Echo`) are. If you want to build
on it as a real, independently-shipped provider:

1. Copy `Community.Steps.JsonRpc` to your own repository under your own namespace
   (never `Platform.Steps.*` / `Platform.Engine.*`).
2. Publish it as a NuGet package under Apache-2.0.
3. List it in the [community provider index](../../registry/README.md) (Community tier),
   or work towards the [Verified-tier rubric](../../VERIFIED_TIER_CHECKLIST.md) if you
   want platform-team endorsement — see `CONTRIBUTING.md` for both paths.
