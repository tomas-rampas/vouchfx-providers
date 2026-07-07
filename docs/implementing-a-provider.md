# Implementing a Custom Step Provider

Welcome to the vouchfx provider authoring guide. This document walks you through building a step provider that integrates with the vouchfx engine — from project setup through testing and submission.

A provider is a compile-time C# plugin that converts a YAML step into an executable C# code fragment. It is **not** dynamic runtime loading; it is **not** sandboxing; it is discovered reflectively at suite startup and integrated into the engine's standard compilation and execution pipeline. This guide shows you how to implement one correctly.

## 1. What a Provider Is

A step in vouchfx has a **type** of the form `<family>.<provider>` — for example, `http.rest`, `db-assert.postgres`, `mq-publish.kafka`. The family names the *intent* (what you are testing: a database, a message queue, an HTTP endpoint); the provider names the *technology* (PostgreSQL, Kafka, REST).

Your provider is a C# class decorated with `[StepProvider]` that:

- **Declares its step kind.** `Kind { get; } = new StepKindId("family", "provider")`
- **Binds YAML** from a `.e2e.yaml` file into a strongly-typed C# model (record)
- **Validates** the model against provider-specific and project-level rules
- **Compiles** the model into a `CsxFragment` — a reusable C# code snippet
- **Is discovered reflectively** by the engine at startup — no manual registration is needed

The engine calls your provider at compile time (once per suite), assembles all providers' CSX fragments into a single Roslyn script, compiles it once, then executes it in an isolated, collectible `AssemblyLoadContext`. Your provider never touches the execution path — it only emits code.

Providers are **compile-time, source-level plugins**. There is no dynamic assembly loading, no sandbox, no inheritance hierarchy you must fit into. Your provider is one of many implementations of a frozen v1 contract; evolution is additive only, via new optional interfaces.

**The three governance tiers** distinguish endorsement and bundling (see `GOVERNANCE.md` for full details):

- **Core** — platform-team authored, bundled with the engine, versioned with the engine
- **Verified** — community-authored, independently versioned, platform-team endorsed after passing a published rubric (conformance tests, security review, documentation)
- **Community** — community-authored, independently versioned, listed in the community registry with no platform endorsement

## 2. What You Can Build Self-Contained

You can author a provider **entirely** in your own repository without modifying the engine if your provider falls into one of these categories:

### Protocol Providers — Direct to System Under Test

Your provider talks directly to a service the test author supplies as an absolute URL or address, resolved at step-execution time. The engine does not manage the infrastructure dependency.

**Examples:**
- `http.rest` — sends HTTP requests to an absolute URL
- `rpc.json-rpc` (the community sample) — sends JSON-RPC 2.0 requests over HTTP
- Any provider that connects to a system by hostname/port/URL with no Aspire dependency management

**What you need:**
- Define the step model (YAML shape)
- Implement the four provider interfaces
- Emit CSX that uses only System.* and user-supplied libraries (see section 6 on reference contributors)
- Write conformance tests (the template's `ProviderTestHarness` pattern)

### Infrastructure Providers — Observing Engine-Managed Dependencies

Your provider observes a dependency type the engine **already manages** via Aspire. The engine's orchestration already knows how to start the container, health-gate it, and expose its connection details via `ScriptGlobalVariables`.

**Managed dependency types (the engine orchestration already supports these):**
- Relational databases: `postgres`, `mysql`, `sqlserver`, `mongodb`
- In-memory stores: `redis`, `elasticsearch`
- Message brokers: `kafka`, `rabbitmq`, `nats`, `azureservicebus`
- File/HTTP services: `mailpit` (SMTP test server)

**Examples:**
- `db-assert.postgres` — executes SQL assertions against PostgreSQL
- `mq-expect.kafka` — consumes and asserts on Kafka messages
- Any provider that reads from a connection string exposed by orchestration

**What you need:**
- All of the above, plus
- Implement `IResourceContributor<TModel>` to declare which dependency you need
- The engine reconciles your declaration against `environment.dependencies` before suite execution

### Providers Requiring New Infrastructure

Your provider needs Aspire to manage a **new container type** not in the list above (for example, a custom gRPC server, a proprietary database, a third-party cloud emulator). **This requires engine-side support.**

The orchestration dependency registry (`Platform.Engine.Orchestration` / `AspireResourceProvider`) is closed. To add support for a new infrastructure type:

1. Fork or clone the engine repository (https://github.com/tomas-rampas/vouchfx)
2. Add a new `AspireResourceProvider<TDependency>` implementation to `src/Engine/Platform.Engine.Orchestration/`
3. Register it in `AspireResourceProviderRegistry`
4. Submit a pull request to the engine repository

This is not a blocker — it is a deliberate separation of concerns. The engine's orchestration layer is the single source of truth for topology setup; providers plug into it, not the other way around. If you need help, ask on GitHub Discussions or open an issue.

## 3. Project Setup

### Create the Provider Project

Copy the template to get started:

```bash
# Navigate to your repository
cd your-provider-repo

# Copy the template and rename
cp -r template/Community.Steps.Hello src/Community.Steps.MyKind
cd src/Community.Steps.MyKind

# Rename the namespace (via find/replace in your IDE)
# Community.Steps.Hello → YourOrg.Steps.YourKind
```

### The `.csproj` File

Your provider project must:
- Reference **only** `Platform.Sdk` (version 1.0.0 or later)
- Use a **non-reserved namespace** (never `Platform.Engine.*` or `Platform.Steps.*`)
- Target `.NET 8.0`

Here is the minimal structure:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>YourOrg.Steps.YourKind</RootNamespace>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- Frozen v1 SDK contract — no engine dependencies needed. -->
    <PackageReference Include="Platform.Sdk" Version="1.0.0" />
  </ItemGroup>

</Project>
```

**Before v1.0 release:** Pack the SDK locally from the engine repository (see `CONTRIBUTING.md` "Building Before v1.0" for the command).

**After v1.0:** Restore from NuGet directly.

### Assembly-Graph Hygiene

The engine refuses provider DLLs that declare the reserved namespaces at startup:

- `Platform.Engine.*` — reserved for engine internals
- `Platform.Steps.*` — reserved for Core providers

Use your own namespace: `MyOrg.Steps.Kafka`, `Community.Steps.Snowflake`, `Example.Steps.Hello`. This prevents collisions when multiple providers are loaded together.

**Why this matters:** Your provider DLL is loaded into the host process alongside others. If two providers both declare `Platform.Steps.Foo`, the assembly-graph loader sees a collision and refuses the suite. Using a unique namespace keeps the host assembly graph clean.

### Build Warnings

The engine's `Directory.Build.props` sets strict compiler flags:

```bash
dotnet build your-provider.csproj /p:TreatWarningsAsErrors=true
```

Your provider must build with **zero warnings**. This is enforced by CI for Verified-tier submissions.

## 4. The Model — Strongly-Typed Step Shape

Your step model is an immutable `record` implementing `IStepModel`. It represents the YAML shape the test author writes.

### Example: JsonRpc Model

From the community sample `Community.Steps.JsonRpc`:

```csharp
using Platform.Sdk;

namespace Community.Steps.JsonRpc;

/// <summary>
/// A single JSONPath assertion evaluated against the JSON-RPC response envelope's result.
/// </summary>
public sealed record JsonRpcResultAssertion(string Path, string ExpectedJson);

/// <summary>
/// The optional expect block for an rpc.json-rpc step.
/// Either Result (a list of JSONPath assertions) or ErrorCode (a negative test), never both.
/// </summary>
public sealed record JsonRpcExpect(
    IReadOnlyList<JsonRpcResultAssertion>? Result = null,
    int? ErrorCode = null);

/// <summary>
/// Strongly-typed model for the rpc.json-rpc step kind (JSON-RPC 2.0 over HTTP).
/// </summary>
public sealed record JsonRpcModel(
    string Url,
    string Method,
    string? ParamsJson,
    bool Notification,
    JsonRpcExpect? Expect) : IStepModel;
```

This maps to YAML like:

```yaml
- id: call-sum
  type: rpc.json-rpc
  url: "http://localhost:8080/rpc"
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

### Rules

1. **Never use `Dictionary<string,object>`** — always a record. The engine enforces this; loosely-typed dictionaries defeat compile-time checking and make validation harder.
2. **Properties are immutable.** Use `record` syntax, not mutable `class`.
3. **One property per YAML field** the author writes (plus the engine's universal fields: `capture`, `verifyMode`, `timeout`, `continueOnFailure`).
4. **Nested structures are nested records.** In the example above, `JsonRpcExpect` and `JsonRpcResultAssertion` are records, not dictionaries.
5. **Strings for template-resolved values.** Fields that support `{placeholder}` or `${secret:source/path}` substitution are strings; their resolution happens at step-execution time, not bind time.

## 5. The Five Contract Surfaces

Your provider class implements up to five interfaces. Four are mandatory; one is optional per step kind.

### Mandatory Interface 1: `IStepProvider`

Identity and metadata:

```csharp
[StepProvider]  // Reflective discovery attribute — required
public sealed class MyKindProvider
    : IStepProvider
{
    public StepKindId Kind { get; } = new StepKindId("family", "provider");

    public ProviderMetadata Metadata { get; } = new ProviderMetadata(
        Version: "1.0.0",
        MinEngineVersion: "1.0.0",
        License: "Apache-2.0",
        Authors: new[] { "you" });
}
```

- **`Kind`** — must be unique across all loaded providers. Use kebab-case for both parts (lowercase, hyphens for word breaks).
- **`MinEngineVersion`** — the earliest engine version your provider supports. Use semantic versioning; the engine enforces compatibility at suite startup.
- **`Metadata`** — used in reporting and documentation. `License` should always be Apache-2.0 for compatibility.

### Mandatory Interface 2: `IStepBinder<TModel>`

Converts YAML into your model and supplies the JSON Schema fragment for IDE validation:

```csharp
public JsonSchemaFragment SchemaFragment { get; } = new JsonSchemaFragment(
    """
    {
      "type": "object",
      "required": ["url", "method"],
      "properties": {
        "url": {
          "description": "Absolute target URL.",
          "type": "string"
        },
        "method": {
          "description": "The method name.",
          "type": "string"
        }
      },
      "additionalProperties": true
    }
    """);

public MyKindModel Bind(YamlNode node, IBindingContext ctx)
{
    if (node is not YamlMappingNode mapping)
        return new MyKindModel(Url: string.Empty, Method: string.Empty);

    var url = GetScalar(mapping, "url") ?? string.Empty;
    var method = GetScalar(mapping, "method") ?? string.Empty;

    return new MyKindModel(Url: url, Method: method);
}

private static string? GetScalar(YamlMappingNode mapping, string key) =>
    mapping.Children.TryGetValue(new YamlScalarNode(key), out var value)
    && value is YamlScalarNode scalar
        ? scalar.Value
        : null;
```

**The Bind → Validate contract:** `Bind` *shapes* YAML into the model; it does **not** reject input. A missing field or type mismatch yields an empty model (e.g., `string.Empty`), which `Validate` then rejects with a clear error. This separation keeps error messages focused and user-friendly.

The **schema fragment** describes *only* your provider's fields (not the universal `type` discriminator — the engine injects that via an `if`/`then` clause). The schema is embedded in the language-wide `.e2e.yaml` schema and enables IDE completions.

### Mandatory Interface 3: `IStepValidator<TModel>`

Validates the model against provider rules and project context:

```csharp
public ValidationResult Validate(MyKindModel model, IProjectContext ctx)
{
    if (string.IsNullOrWhiteSpace(model.Url))
    {
        return ValidationResult.Failure(
            "my-kind: 'url' must not be empty or whitespace.");
    }

    if (string.IsNullOrWhiteSpace(model.Method))
    {
        return ValidationResult.Failure(
            "my-kind: 'method' must not be empty or whitespace.");
    }

    return ValidationResult.Success;
}
```

**What belongs in `Validate`:**
- Empty-field checks (model fields required by your provider)
- Mutual-exclusion checks (e.g., "both `expect.result` and `expect.error` declared" → reject)
- Structural rules (e.g., "params must be a mapping or array, not a scalar")
- Cross-field constraints (e.g., "`notification: true` with `expect:` → reject")

**What `Validate` cannot see:**
- The `capture` map (only `ICompileContext` sees it)
- The step's `id`, `verifyMode`, `timeout`, `continueOnFailure` (universal fields, the engine owns them)
- The suite's `variables` or prior step outputs (those are runtime, not compile-time)

If you need to validate something involving `capture`, defer that check to `Emit` time and produce a fail-safe block (see section 7 on verdicts).

### Mandatory Interface 4: `IStepCompiler<TModel>`

Emits the `CsxFragment` — the executable C# code that runs at suite execution:

```csharp
public CsxFragment Emit(MyKindModel model, ICompileContext ctx)
{
    var safeId = CsxFragment.SanitiseId(ctx.StepId);

    const string helper = """
        static class MyKind_Helpers
        {
            public static bool Check(string value, string expected)
                => string.Equals(value, expected, System.StringComparison.Ordinal);
        }
        """;

    var block = $$"""
        {
            var __sw_{{safeId}} = System.Diagnostics.Stopwatch.StartNew();
            var __value_{{safeId}} = {{JsonSerializer.Serialize(model.Value)}};
            __sw_{{safeId}}.Stop();

            var __verdict_{{safeId}} =
                MyKind_Helpers.Check(__value_{{safeId}}, "expected")
                    ? Platform.Engine.Abstractions.Verdict.Pass
                    : Platform.Engine.Abstractions.Verdict.Fail;

            Vars[Platform.Engine.Abstractions.VarKeys.Outcome("{{safeId}}")] =
                new Platform.Engine.Abstractions.StepOutcome(
                    __verdict_{{safeId}},
                    __sw_{{safeId}}.ElapsedMilliseconds,
                    "{}");
        }
        """;

    return new CsxFragment(
        RequiredUsings: new[] { "System" },
        RequiredHelpers: new[] { helper },
        StatementBlock: block);
}
```

Key points:
- `ctx.StepId` is the step's id from the YAML (may contain hyphens)
- `ctx.CaptureExprs` is a dictionary of `capture` fields if the step declares captures
- You must write exactly one `StepOutcome` to `Vars[VarKeys.Outcome(safeId)]` every time the block runs
- The verdict is one of the four taxonomy outcomes (see section 7)

### Optional Interface: `ICompileReferenceContributor`

Declare extra compile-time assembly references your emitted code needs.

The engine's minimal Roslyn reference set includes only System.Private.CoreLib, System.Runtime, System.Collections, and System.Text.RegularExpressions. If your code needs `System.Net.Http`, `JsonPath.Net`, or any other library, you must declare it:

```csharp
public IEnumerable<System.Reflection.Assembly> CompileReferenceAssemblies
{
    get
    {
        yield return typeof(System.Net.Http.HttpClient).Assembly;
        yield return typeof(System.Text.Json.Nodes.JsonNode).Assembly;
        yield return typeof(Json.Path.JsonPath).Assembly;
    }
}
```

The engine's `RoslynScriptCompiler` includes your declared assemblies when compiling the joined CSX. **You must declare every type your helper methods or statement block reference by fully-qualified name.**

The JsonRpc sample implements this because its emitted code calls `HttpClient`, `JsonNode`, and `JsonPath.Net`.

### Optional Interface: `IResourceContributor<TModel>`

Declare which Aspire-managed infrastructure dependency your provider needs. The engine reconciles your declarations against `environment.dependencies` before starting the topology:

```csharp
public IResourceBuilder? ContributeResource(MyKindModel model, IHostResourceContext ctx)
{
    // Your step says it needs "postgres:my-db". Tell the engine to wait for that
    // resource and health-gate it before running any steps.
    return ctx.GetManagedResource("my-db");
}
```

**When to implement:** Only if your provider needs Aspire-managed infrastructure (databases, brokers, etc.). Protocol providers (talking to URLs) do not implement this.

### Optional Interface: `IStepDiffRenderer`

Render an expected-vs-observed diff for display in terminals and HTML reports. Implement this if your provider produces observation objects that have a natural "expected" and "actual" shape:

```csharp
public string? RenderDiff(string? observation)
{
    // If observation is JSON with an expected/actual shape, render it nicely
    // as a markdown table or diff. Return null to use the default observation display.
    // See the Core mail-expect.smtp provider for a worked example.
    return null;
}
```

This is rarely needed. Only implement it if your step produces structured assertions where a diff makes sense (e.g., "expected 5, got 7").

## 6. Emitting Correct CSX — The Composition Rules

Your `Emit` method produces a `CsxFragment`. The CSX (C# step eXecution code) is assembled alongside contributions from other providers, so collision prevention is critical. The engine enforces strict composition rules:

### Rule 1: `RequiredUsings` — Bare Namespace Strings Only

```csharp
RequiredUsings: new[] { "System", "System.Diagnostics", "System.Net.Http" }
```

- Must be bare namespace strings (e.g., `"System.Net.Http"`)
- Never include the `using` keyword or a trailing semicolon
- The engine deduplicates and emits the `using` lines at the script top
- Providers must not use `using var` in the statement block (parse error in Roslyn scripts) — use plain `var` + explicit `.Dispose()` in a `finally`

### Rule 2: `RequiredHelpers` — Provider-ID-Prefixed Nested Static Classes

```csharp
RequiredHelpers: new[] {
    """
    static class MyKind_Helpers
    {
        public static bool Check(string a, string b)
            => string.Equals(a, b, System.StringComparison.Ordinal);
    }
    """
}
```

- Must be exactly one nested `static class` definition per entry (full source)
- Class name **must** be prefixed with your provider id followed by an underscore: `MyKind_Helpers`, `RpcJsonRpc_Helpers`
- The engine deduplicates helpers by declared class name
- Two steps of the same kind in one suite **must** produce byte-identical helper source — a static helper must never vary based on step-specific data

**Why the prefix?** If two providers both emit `static class Helpers { }`, the assembly linker sees a collision. The prefix prevents it: `MyKind_Helpers` and `OtherKind_Helpers` are distinct.

**Why byte-identical?** The engine de-duplicates on exact string match. If step 1 emits `static class MyKind_Helpers { public static void Foo() => Bar(1); }` and step 2 emits `static class MyKind_Helpers { public static void Foo() => Bar(2); }`, the engine sees a class-name collision and de-duplicates to one definition — resulting in incorrect behaviour. **Always put step-specific data into the statement block, never into the helper source.** The JsonRpc sample shows this pattern: the helper is `byte-identical` across all steps; the block splices in step-specific values.

### Rule 3: `StatementBlock` — One Brace-Enclosed Block, C# 11 Double-Dollar Raw Strings

```csharp
var block = $$"""
{
    var __sw_{{safeId}} = System.Diagnostics.Stopwatch.StartNew();
    // ... step-specific code ...
    __sw_{{safeId}}.Stop();

    Vars[Platform.Engine.Abstractions.VarKeys.Outcome("{{safeId}}")] =
        new Platform.Engine.Abstractions.StepOutcome(
            __verdict_{{safeId}},
            __sw_{{safeId}}.ElapsedMilliseconds,
            observation);
}
""";
```

**Rules:**
- Must start with `{` and end with `}`
- Must not contain inline `using` directives
- Must not contain `using var` (parse error in Roslyn script bodies)
- Build as a **C# 11 double-dollar raw string** (`$$"""…"""`)

**Why double-dollar?**

With `$$"""…"""`:
- A single `{` or `}` is a **literal brace** (so the block's own braces pass through verbatim)
- `{{hole}}` is an **interpolation hole** the engine fills at emit time

With `$"""…"""` (single-dollar — **wrong**):
- A single `{` is an **interpolation hole** (broken)
- `{{` is a **literal brace** (wrong nesting)

Always use `$$`. If your template contains any braces of its own (e.g., a helper's `static class { … }`), the single-dollar form will produce incorrect output.

### Rule 4: Step-ID Sanitisation

Hyphens are legal in YAML step ids but illegal in C# identifiers:

```csharp
var safeId = CsxFragment.SanitiseId(ctx.StepId);  // "my-step-id" → "my_step_id"

// Then splice the safe id into variable names:
var block = $$"""
{
    var __value_{{safeId}} = 42;  // __value_my_step_id (correct)
}
""";
```

Call `CsxFragment.SanitiseId(stepId)` on any step-id-derived identifier before splicing it into the block.

### Rule 5: Cross-Step State via `Vars` Only

Read previous steps' captured output from `Vars`; write your outcome there:

```csharp
// Read a prior step's captured value:
var priorValue = Vars.TryGetValue("step-1::result", out var raw) ? raw?.ToString() : null;

// Write your outcome:
Vars[Platform.Engine.Abstractions.VarKeys.Outcome(safeId)] =
    new Platform.Engine.Abstractions.StepOutcome(…);

// Capture a value for later steps:
Vars["my-step::extracted"] = extractedValue;
```

Never assume that variables declared by another provider are in the C# scope. The engine assembles all providers' statement blocks into one method; name collisions could otherwise corrupt behaviour. Always route cross-step state through `Vars`.

### No `using var` — Explicit `.Dispose()`

```csharp
// ❌ WRONG — parse error in Roslyn scripts
var block = $$"""
{
    using var client = new System.Net.Http.HttpClient();
}
""";

// ✅ CORRECT — plain var + explicit Dispose in finally
var block = $$"""
{
    var client = new System.Net.Http.HttpClient();
    try
    {
        // use client
    }
    finally
    {
        client?.Dispose();
    }
}
""";
```

The Roslyn script parser does not support `using var` syntax (even though the host application is .NET 8). Always use plain `var` and explicit disposal.

### Engine Type References

Your emitted code refers to engine types by fully-qualified name:

```csharp
// Never: using Platform.Engine.Abstractions;
// Instead, splice fully-qualified names:

var block = $$"""
{
    var __verdict_{{safeId}} =
        someCondition
            ? Platform.Engine.Abstractions.Verdict.Pass
            : Platform.Engine.Abstractions.Verdict.Fail;

    Vars[Platform.Engine.Abstractions.VarKeys.Outcome("{{safeId}}")] =
        new Platform.Engine.Abstractions.StepOutcome(…);
}
""";
```

Your provider does not reference `Platform.Engine.Abstractions` (its `using` would bind to it at compile time, creating a static link that bridges the collectible `AssemblyLoadContext` boundary — breaking the memory model). The emitted script references it by name; the engine already has it in scope when it compiles the joined CSX.

## 7. Verdicts — Taxonomy and Exception Handling

The engine defines four verdicts (not three, not five):

- **`Pass`** — the step's assertions matched; the system behaved as expected
- **`Fail`** — the step's assertions did not match; the system behaved unexpectedly (a product defect)
- **`EnvironmentError`** — infrastructure failed (container unhealthy, network unreachable, database unavailable); not a product defect
- **`Inconclusive`** — the test could not reach a decision (timeout, unmet capture, upstream stall); neither a defect nor an environment error

**Why four?** Conflating an environment error with a defect destroys trust in the tool. A developer sees a Fail verdict and investigates the code. If it was actually a Docker pull timeout, the investigation is wasted. CI's default failure threshold (by default, only Fail breaks the build; Inconclusive does not) depends on this distinction.

### The Exception → Verdict Mapping

Wrap your operation in a try-catch and map exceptions to verdicts:

```csharp
var block = $$"""
{
    var __sw_{{safeId}} = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        // Your provider-specific operation
        var response = await MyKind_Helpers.DoSomethingAsync(…);

        // Assertion logic
        var __pass_{{safeId}} = response.IsSuccess;
        __sw_{{safeId}}.Stop();

        var __verdict_{{safeId}} = __pass_{{safeId}}
            ? Platform.Engine.Abstractions.Verdict.Pass
            : Platform.Engine.Abstractions.Verdict.Fail;

        var __observation_{{safeId}} = /* structured JSON string */;
    }
    catch (System.Net.Http.HttpRequestException ex)
    when (ex.InnerException is System.Net.Sockets.SocketException)
    {
        // Connection refused / DNS failure → environment error
        __sw_{{safeId}}.Stop();
        __verdict_{{safeId}} = Platform.Engine.Abstractions.Verdict.EnvironmentError;
        __observation_{{safeId}} = {{ JsonSerializer.Serialize(new {{ error = "network unreachable" }}) }};
    }
    catch (System.OperationCanceledException)
    {
        // Client-side timeout → inconclusive
        __sw_{{safeId}}.Stop();
        __verdict_{{safeId}} = Platform.Engine.Abstractions.Verdict.Inconclusive;
        __observation_{{safeId}} = {{ JsonSerializer.Serialize(new {{ timeout = true }}) }};
    }
    catch (System.Text.Json.JsonException)
    {
        // Response body is not valid JSON → environment error
        __sw_{{safeId}}.Stop();
        __verdict_{{safeId}} = Platform.Engine.Abstractions.Verdict.EnvironmentError;
        __observation_{{safeId}} = {{ JsonSerializer.Serialize(new {{ badJson = true }}) }};
    }

    Vars[Platform.Engine.Abstractions.VarKeys.Outcome("{{safeId}}")] =
        new Platform.Engine.Abstractions.StepOutcome(
            __verdict_{{safeId}},
            __sw_{{safeId}}.ElapsedMilliseconds,
            __observation_{{safeId}});
}
""";
```

**Guidelines:**
- Network errors (socket exceptions, DNS failures, TLS failures, timeouts) → `EnvironmentError` or `Inconclusive`
- Malformed responses (bad JSON, missing fields, type mismatches) → `EnvironmentError`
- Assertion mismatches (expected 5, got 7) → `Fail`
- Timeouts (client-side, external cancellation, step timeout) → `Inconclusive`

See the JsonRpc sample's README "Verdict-mapping table" for a comprehensive decision tree.

### Engine-Owned RETRY

If the step declares `verifyMode: RETRY`, the engine wraps your statement block in a polling loop (via `Platform.Engine.Abstractions.Retry.RetryRunner`). Your block runs unchanged, multiple times, until it passes or `timeout` elapses.

**You do not implement polling yourself.** Write a re-runnable block that:
- Writes a verdict on every invocation
- Uses `Fail` (never `Inconclusive`) for "not yet satisfied" assertions
- The engine's `RetryRunner` converts a sustained `Fail` into `Inconclusive` once the timeout window elapses

This means your block must be idempotent — it should produce consistent results when run multiple times against the same system state.

## 8. Placeholders and Secrets

YAML fields may contain template references resolved at step-execution time:
- `{placeholder}` — replaced from `Vars`
- `${secret:source/path}` — replaced from the secrets subsystem

Your provider must resolve **every string field** the author might write (url, method, params values, headers, etc.) using `Secret_Helpers.ResolveTemplate`:

```csharp
// From Platform.Engine.Abstractions (available in emitted code by FQN)
var resolved = await Platform.Engine.Abstractions.SecretHelper.ResolveTemplate(
    template: model.Url,  // e.g., "http://{host}:{port}/api"
    vars: Vars,
    secrets: Secrets,
    cancellationToken: cancellationToken);
```

**Important:** Resolved values **must never reach observations, exceptions, or logs.** If a secret resolves to a password, it must never appear in a failure message or observation JSON.

The engine provides `SecretString` — a type with no value-returning `ToString()` method — for this purpose. But the simpler pattern is: **capture only the reference, never the value**:

```csharp
// ❌ WRONG — the password is baked into the observation
var observation = $"{{\"password\": \"{resolvedSecret}\"}}";

// ✅ CORRECT — record only that a secret was resolved
var observation = $"{{\"usedSecret\": true}}";
```

If your step must capture a resolved value (e.g., a bearer token used to construct a request header), use `SecretString`:

```csharp
// Store in Vars — the SecretString prevents accidental disclosure
var secretToken = new Platform.Engine.Abstractions.Secrets.SecretString(resolvedToken);
Vars["auth::bearer"] = secretToken;
```

**Walk string leaves, not raw JSON:** If `model.Params` is a JSON string, parse it into a tree, resolve each string leaf individually, then re-serialise:

```csharp
var paramsJson = "{ \"key\": \"${secret:api-key}\", \"value\": \"${secret:api-value}\" }";

// ❌ WRONG — template-substitutes the raw JSON text, corrupts structure if value contains quotes
var resolved = await ResolveTemplate(paramsJson, …);

// ✅ CORRECT — parse tree, resolve leaves, re-serialise
var paramsNode = JsonNode.Parse(paramsJson);
foreach (var prop in paramsNode.AsObject())
{
    if (prop.Value is JsonValue scalar)
    {
        var leaf = scalar.GetValue<string>();
        prop.Value = JsonValue.Create(
            await ResolveTemplate(leaf, …));
    }
}
var resolvedJson = paramsNode.ToJsonString();
```

The JsonRpc sample implements this pattern for `params`.

## 9. Capture — JSONPath Evaluation into Vars

The engine's universal `capture` field (DSL §6.1) lets the test author extract values from your step's response into `Vars` for later steps to use.

Your provider must evaluate `capture` expressions against your response and write the results to `Vars`:

```csharp
// ctx.CaptureExprs is IReadOnlyDictionary<string, CaptureExpr>
// where each CaptureExpr carries the type (JSONPath or XPath) and the expression

foreach (var (varName, captureExpr) in ctx.CaptureExprs)
{
    try
    {
        // For a JSON response, evaluate the JSONPath expression
        var results = JsonPath.JsonPath.Select(responseJson, captureExpr.Expression);
        
        // Write the matched value(s) to Vars
        // If multiple matches, store as a list or JSON array
        var matched = results.FirstOrDefault()?.GetValue<object>();
        if (matched is not null)
        {
            Vars[varName] = matched;
        }
        else
        {
            // No match → this is not a Fail; it downgrades an otherwise-Pass to Inconclusive
            // Write a flag so the post-step logic can detect this
            Vars["__captureUnmet::" + varName] = true;
        }
    }
    catch (Exception ex)
    {
        // JSONPath parse error → environment error
        __verdict_{{safeId}} = Platform.Engine.Abstractions.Verdict.EnvironmentError;
        __observation_{{safeId}} = …;
    }
}

// After all captures:
// If any capture was unmet AND the primary assertion passed, downgrade to Inconclusive
if (__verdict_{{safeId}} == Platform.Engine.Abstractions.Verdict.Pass
    && Vars.Keys.Any(k => k.StartsWith("__captureUnmet::", StringComparison.Ordinal)))
{
    __verdict_{{safeId}} = Platform.Engine.Abstractions.Verdict.Inconclusive;
    __observation_{{safeId}} = { "captureUnmet": "..." };
}
```

The engine's framework handles capture evaluation for you in many cases, but if your provider does custom response parsing (not just JSON), you may need to implement this yourself. See the Core `http.rest` provider for the full pattern.

## 10. Testing Your Provider

Testing proves your provider works end-to-end through the full engine pipeline.

### Conformance Tests: `ProviderTestHarness`

For dependency-free providers (no infrastructure), use the published `ProviderTestHarness.RunSingleStepAsync()`:

```csharp
using Platform.Sdk.Testing;
using Xunit;

namespace Community.Steps.Hello.Tests;

public sealed class HelloConsoleProviderTests
{
    /// <summary>
    /// End-to-end: a hello.console step runs through schema validation, binding,
    /// validation, compilation, and isolated execution, resolving to Pass.
    /// </summary>
    [Fact]
    public async Task Conformance_HelloConsoleStep_MatchingExpect_RunsEndToEnd_Pass()
    {
        const string yaml = """
            steps:
              - id: say-hello
                type: hello.console
                message: "hello, community"
                expect: "hello, community"
            """;

        var result = await ProviderTestHarness.RunSingleStepAsync(
            yaml,
            typeof(HelloConsoleProvider).Assembly,
            stepId: "say-hello");

        Assert.Empty(result.SchemaErrors);
        Assert.Empty(result.ValidationErrors);
        Assert.True(result.IsPass, $"Expected Pass, got {result.Verdict}");
    }

    /// <summary>
    /// Fail path: when expectations do not match, verdict is Fail (not an exception).
    /// </summary>
    [Fact]
    public async Task Conformance_HelloConsoleStep_MismatchedExpect_RunsEndToEnd_Fail()
    {
        const string yaml = """
            steps:
              - id: say-hello
                type: hello.console
                message: "hello"
                expect: "goodbye"
            """;

        var result = await ProviderTestHarness.RunSingleStepAsync(
            yaml,
            typeof(HelloConsoleProvider).Assembly,
            stepId: "say-hello");

        Assert.Equal(Verdict.Fail, result.Verdict);
    }

    /// <summary>
    /// Schema gate: a step without required fields fails schema validation,
    /// never reaching the provider.
    /// </summary>
    [Fact]
    public async Task Conformance_HelloConsoleStep_MissingMessage_FailsSchemaValidation()
    {
        const string yaml = """
            steps:
              - id: say-hello
                type: hello.console
                expect: "hello"
            """;

        var result = await ProviderTestHarness.RunSingleStepAsync(
            yaml,
            typeof(HelloConsoleProvider).Assembly,
            stepId: "say-hello");

        Assert.NotEmpty(result.SchemaErrors);
        Assert.Null(result.Verdict);
    }
}
```

### Custom Harness: When Your Provider Needs Extra References

If your provider implements `ICompileReferenceContributor` (because your emitted code calls `HttpClient`, `JsonPath.Net`, etc.), `ProviderTestHarness` will fail compilation — it does not include your contributed references.

Use the custom-harness pattern (modelled on the engine's own `HttpRestExecutionTests`), as demonstrated in the JsonRpc sample (`Community.Steps.JsonRpc.Tests/JsonRpcHarness.cs`):

```csharp
internal static class MyKindHarness
{
    private static readonly IReadOnlyList<string> AdditionalReferencePaths =
        new MyKindProvider().CompileReferenceAssemblies
            .Select(a => a.Location)
            .ToArray();

    public static async Task<Result> RunAsync(
        MyKindModel model,
        string stepId,
        bool retry = false,
        long? timeoutMs = null,
        IReadOnlyDictionary<string, CaptureExpr>? captures = null)
    {
        var provider = new MyKindProvider();

        var validation = provider.Validate(model, new TestProjectContext());
        if (!validation.IsValid)
            throw new InvalidOperationException($"Invalid model: {string.Join("; ", validation.Errors)}");

        var compileCtx = new TestCompileContext(stepId, captureExprs: captures);
        var fragment = provider.Emit(model, compileCtx);

        var plan = new StepCompilePlan(stepId, fragment, retry, timeoutMs, null);
        var assembled = CsxAssembler.Assemble(new[] { plan });

        // This is the key difference: pass additionalReferencePaths
        var compiled = RoslynScriptCompiler.CompileOnce(
            assembled.CsxSource,
            additionalReferencePaths: AdditionalReferencePaths);

        var vars = new Dictionary<string, object?>(StringComparer.Ordinal);
        var globals = new ScriptGlobalVariables(vars);

        await RoslynScriptCompiler.RunIsolatedAsync(compiled, globals, runLabel: stepId);

        var safeId = CsxFragment.SanitiseId(stepId);
        var outcomeKey = VarKeys.Outcome(safeId);

        if (!vars.TryGetValue(outcomeKey, out var raw) || raw is not StepOutcome outcome)
            throw new InvalidOperationException($"No outcome written for step '{stepId}'");

        return new Result(outcome.Verdict, outcome.Observation, outcome.DurationMs, vars);
    }

    public sealed record Result(
        Verdict Verdict,
        string? Observation,
        long DurationMs,
        IReadOnlyDictionary<string, object?> Vars);
}
```

### Unit Tests: CSX Composition

Test that your `Emit` produces a well-formed `CsxFragment`:

```csharp
[Fact]
public void Unit_Emit_FragmentSatisfiesCompositionRules()
{
    var provider = new MyKindProvider();
    var model = new MyKindModel(…);
    var ctx = new TestCompileContext(stepId: "test-step");

    var fragment = provider.Emit(model, ctx);

    // RequiredUsings: bare namespace strings, no inline using
    Assert.All(fragment.RequiredUsings, u =>
        Assert.False(u.TrimStart().StartsWith("using ", StringComparison.Ordinal)));

    // RequiredHelpers: provider-id-prefixed class name
    Assert.Contains(
        fragment.RequiredHelpers,
        h => h.Contains("MyKind_Helpers", StringComparison.Ordinal));

    // StatementBlock: brace-enclosed, writes VarKeys.Outcome
    Assert.StartsWith("{", fragment.StatementBlock.TrimStart());
    Assert.EndsWith("}", fragment.StatementBlock.TrimEnd());
    Assert.Contains("VarKeys.Outcome", fragment.StatementBlock);

    // No using var
    Assert.DoesNotContain("using var", fragment.StatementBlock);
}

[Fact]
public void Unit_Emit_HyphenatedStepId_SanitisedInBlock()
{
    var provider = new MyKindProvider();
    var model = new MyKindModel(…);
    var ctx = new TestCompileContext(stepId: "my-step-id");

    var fragment = provider.Emit(model, ctx);

    // The block must use the sanitised id (hyphens → underscores)
    Assert.Contains("my_step_id", fragment.StatementBlock);
    Assert.DoesNotContain("my-step-id", fragment.StatementBlock);
}
```

### Docker Integration Tests

For infrastructure providers, create integration tests that start a real Docker container:

```csharp
[Collection("Docker")]  // Serialise Docker tests
public sealed class MyKindProviderIntegrationTests : IAsyncLifetime
{
    private ITestcontainersContainer? _container;

    public async Task InitializeAsync()
    {
        // Start a real container (e.g., PostgreSQL, Kafka)
        _container = new ContainerBuilder()
            .WithImage("my-org/my-kind:latest")
            .WithPortBinding(8080, 8080)
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.StopAsync();
    }

    [Fact]
    public async Task Integration_MyKindStep_ConnectsAndAsserts()
    {
        var model = new MyKindModel(Url: $"http://localhost:8080", …);
        var result = await MyKindHarness.RunAsync(model, "test-step");

        Assert.Equal(Verdict.Pass, result.Verdict);
    }
}
```

### Running Tests Locally

```bash
# All tests (including Docker)
dotnet test your-provider.Tests

# Only non-Docker tests (for CI-free iteration)
dotnet test your-provider.Tests -c Release --filter "requires!=docker"
```

## 11. Publishing and Submission

Once your provider is tested and documented, you have two paths:

### Community Tier — Index Listing

**When to choose this:** Your provider is authored, tested, and published on NuGet. You want discoverability without seeking platform endorsement.

**How to submit:**

1. **Publish to NuGet** — pack your provider as a NuGet package and push it to nuget.org:

   ```bash
   dotnet pack your-provider/your-provider.csproj -c Release -o ./nupkg
   dotnet nuget push nupkg/YourOrg.Steps.YourKind.1.0.0.nupkg -k <api-key> -s https://api.nuget.org/v3/index.json
   ```

2. **Add to the community index** — open a GitHub issue using the **Provider Listing** template, or submit a pull request to `registry/community-providers.json` following the schema in `registry/community-providers.schema.json`.

3. **List immediately** — a maintainer adds your provider to the registry. There is no conformance testing for Community tier; only Apache-2.0 compliance and the reflective-discovery contract.

### Verified Tier — Platform Endorsement

**When to choose this:** Your provider meets (or nearly meets) the Verified-tier rubric (see `VERIFIED_TIER_CHECKLIST.md`). You want platform-team endorsement and website listing.

**The rubric (summary):**

1. Integration-test fixture passes on the engine main branch + two preceding minors
2. README with ≥3 worked examples + known-limitations section
3. Security sign-off: credentials, transitive CVEs, TLS, no telemetry, package signature
4. Apache-2.0 license + all commits signed off via DCO (`git commit -s`)
5. `MinEngineVersion` declared in provider metadata
6. CSX code reviewed for §13.3.1 conformance by a maintainer

**How to submit:**

1. **Signal intent (recommended)** — open a GitHub issue using the **Verified Proposal** template. Maintainers will prioritise review bandwidth and give you early feedback.

2. **Prepare your submission:**

   ```
   verified/my-kind-provider/
   ├── src/
   │   ├── Community.Steps.MyKind.csproj
   │   ├── MyKindProvider.cs
   │   ├── MyKindModel.cs
   │   └── …
   ├── tests/
   │   ├── Community.Steps.MyKind.Tests.csproj
   │   ├── MyKindProviderTests.cs
   │   └── …
   └── README.md
   ```

3. **Open a pull request** — fork this repository, commit your submission, and open a PR using the **Verified Submission** template. Complete the checklist (all six rubric items).

4. **CI conformance gate runs automatically:**
   - Compile your provider against `Platform.Sdk`
   - Run your test suite
   - Validate your JSON Schema fragment

   **All tests must pass.** If any fail, push fixes to your PR branch; CI re-runs automatically.

5. **Maintainer review:**
   - **Security review** — credentials, CVEs, TLS, telemetry, package signature
   - **CSX review** — read the generated C# code; confirm it follows §13.3.1
   - **Conformance validation** — verify your fixture also passes on engine main + two preceding minors (human validation; CI runs only main)

6. **Merge and promotion** — upon approval, the PR merges. Your provider is now Verified, website-listed, and discoverable via the public registry.

## 12. Submission Checklist

Before opening a Community or Verified submission:

- [ ] **Namespace:** My provider uses a non-reserved namespace (never `Platform.Engine.*` or `Platform.Steps.*`)
- [ ] **Four interfaces:** My provider implements `IStepProvider`, `IStepBinder<TModel>`, `IStepValidator<TModel>`, `IStepCompiler<TModel>`
- [ ] **Model:** My step model is a strongly-typed record, never `Dictionary<string,object>`
- [ ] **CSX composition:**
  - [ ] `RequiredUsings` is bare namespace strings only
  - [ ] `RequiredHelpers` contains one provider-id-prefixed nested static class
  - [ ] `StatementBlock` is one brace-enclosed block, built as a `$$"""…"""` raw string
  - [ ] No `using var` in the body
  - [ ] Step id is sanitised via `CsxFragment.SanitiseId`
  - [ ] Cross-step state passes only through `Vars`
- [ ] **Verdicts:** My provider maps exceptions to the four-outcome taxonomy (Pass, Fail, EnvironmentError, Inconclusive)
- [ ] **Secrets:** Every author-facing string field is resolved via `Secret_Helpers.ResolveTemplate`; secrets never appear in observations
- [ ] **Capture:** My provider evaluates `capture` expressions against the response and writes results to `Vars`
- [ ] **References:** If my emitted code calls types outside System.*, I implement `ICompileReferenceContributor`
- [ ] **Tests:**
  - [ ] Conformance tests drive the full pipeline (schema-validate → parse → bind → validate → emit → compile → execute)
  - [ ] At least one Pass path and one Fail path are tested
  - [ ] Unit tests confirm CSX composition rules are satisfied
  - [ ] Integration tests (if applicable) run Docker fixtures
  - [ ] All tests pass: `dotnet test -c Release --filter "requires!=docker"`
- [ ] **Documentation:**
  - [ ] `README.md` exists and contains ≥3 worked `.e2e.yaml` examples
  - [ ] Known limitations are documented
  - [ ] The provider's step type, model shape, and typical use cases are clear
- [ ] **Licensing:** My provider is Apache-2.0 licensed (or compatible)
- [ ] **Commits:** All commits are signed off via DCO: `git commit -s`
- [ ] **Build:** My provider builds with zero warnings: `dotnet build /p:TreatWarningsAsErrors=true`
- [ ] **Metadata:** `MinEngineVersion` is declared in provider metadata

For Verified submissions:

- [ ] I have read the CsxFragment composition rules (blueprint §13.3.1) and confirmed my provider follows them
- [ ] I have read the security review checklist (VERIFIED_TIER_CHECKLIST.md)
- [ ] My fixture passes locally and on the engine main branch

---

**Welcome to the vouchfx provider ecosystem.** Thank you for contributing.
