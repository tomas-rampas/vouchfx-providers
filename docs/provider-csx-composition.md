# CSX Composition, Verdicts, Secrets and Capture

**Stage 4 of the provider authoring journey**

## Emitting Correct CSX — The Composition Rules

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

**Why byte-identical?** The engine de-duplicates on exact string match. If step 1 emits `static class MyKind_Helpers { public static void Foo() => Bar(1); }` and step 2 emits `static class MyKind_Helpers { public static void Foo() => Bar(2); }`, the engine sees a class-name collision and de-duplicates to one definition — resulting in incorrect behaviour. **Always put step-specific data into the statement block, never into the helper source.**

### Rule 3: `StatementBlock` — One Brace-Enclosed Block, C# 11 Double-Dollar Raw Strings

```csharp
var block = $$"""
{
    var __sw_{{safeId}} = System.Diagnostics.Stopwatch.StartNew();
    // ... step-specific code, ending by assigning these two ...
    var __verdict_{{safeId}} = Vouchfx.Engine.Abstractions.Verdict.Pass;
    var __observation_{{safeId}} = "{}";
    __sw_{{safeId}}.Stop();

    Vars[Vouchfx.Engine.Abstractions.VarKeys.Outcome("{{safeId}}")] =
        new Vouchfx.Engine.Abstractions.StepOutcome(
            __verdict_{{safeId}},
            __sw_{{safeId}}.ElapsedMilliseconds,
            __observation_{{safeId}});
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
Vars[Vouchfx.Engine.Abstractions.VarKeys.Outcome(safeId)] =
    new Vouchfx.Engine.Abstractions.StepOutcome(…);

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
// Never: using Vouchfx.Engine.Abstractions;
// Instead, splice fully-qualified names:

var block = $$"""
{
    var __verdict_{{safeId}} =
        someCondition
            ? Vouchfx.Engine.Abstractions.Verdict.Pass
            : Vouchfx.Engine.Abstractions.Verdict.Fail;

    Vars[Vouchfx.Engine.Abstractions.VarKeys.Outcome("{{safeId}}")] =
        new Vouchfx.Engine.Abstractions.StepOutcome(…);
}
""";
```

Your provider does not reference `Vouchfx.Engine.Abstractions` (its `using` would bind to it at compile time, creating a static link that bridges the collectible `AssemblyLoadContext` boundary — breaking the memory model). The emitted script references it by name; the engine already has it in scope when it compiles the joined CSX.

## Verdicts — Taxonomy and Exception Handling

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
    var __verdict_{{safeId}} = Vouchfx.Engine.Abstractions.Verdict.EnvironmentError;
    var __observation_{{safeId}} = "{\"error\":\"unexpected\"}";

    try
    {
        // Your provider-specific operation
        var response = await MyKind_Helpers.DoSomethingAsync();

        // Assertion logic
        var __pass_{{safeId}} = response.IsSuccess;
        __verdict_{{safeId}} = __pass_{{safeId}}
            ? Vouchfx.Engine.Abstractions.Verdict.Pass
            : Vouchfx.Engine.Abstractions.Verdict.Fail;
        __observation_{{safeId}} = __pass_{{safeId}} ? "{\"matched\":true}" : "{\"matched\":false}";
    }
    catch (System.Net.Http.HttpRequestException ex)
    when (ex.InnerException is System.Net.Sockets.SocketException)
    {
        // Connection refused / DNS failure → environment error
        __verdict_{{safeId}} = Vouchfx.Engine.Abstractions.Verdict.EnvironmentError;
        __observation_{{safeId}} = "{\"error\":\"network unreachable\"}";
    }
    catch (System.OperationCanceledException)
    {
        // Client-side timeout → inconclusive
        __verdict_{{safeId}} = Vouchfx.Engine.Abstractions.Verdict.Inconclusive;
        __observation_{{safeId}} = "{\"timeout\":true}";
    }
    catch (System.Text.Json.JsonException)
    {
        // Response body is not valid JSON → environment error
        __verdict_{{safeId}} = Vouchfx.Engine.Abstractions.Verdict.EnvironmentError;
        __observation_{{safeId}} = "{\"badJson\":true}";
    }
    finally
    {
        __sw_{{safeId}}.Stop();
    }

    Vars[Vouchfx.Engine.Abstractions.VarKeys.Outcome("{{safeId}}")] =
        new Vouchfx.Engine.Abstractions.StepOutcome(
            __verdict_{{safeId}},
            __sw_{{safeId}}.ElapsedMilliseconds,
            __observation_{{safeId}});
}
""";
```

Declaring `__verdict_{{safeId}}` and `__observation_{{safeId}}` **before** the `try` (mirroring how the JSON-RPC provider declares its own `verdict`/`observation` locals ahead of its `try` block) is what makes the catch clauses' plain assignments (no `var`) legal — declaring them inside the `try` and assigning from a `catch` would be a compile error (CS0103, the name would not exist in the catch's scope). Each `__observation_{{safeId}}` value is a literal escaped-JSON string spliced directly into the emitted source (single braces are literal in a `$$"""` raw string), never an emit-time `JsonSerializer.Serialize(new { ... })` call nested inside a `{{ }}` hole — nesting object-initialiser braces inside an interpolation hole does not parse.

**Guidelines:**
- Network errors (socket exceptions, DNS failures, TLS failures, timeouts) → `EnvironmentError` or `Inconclusive`
- Malformed responses (bad JSON, missing fields, type mismatches) → `EnvironmentError`
- Assertion mismatches (expected 5, got 7) → `Fail`
- Timeouts (client-side, external cancellation, step timeout) → `Inconclusive`

### Engine-Owned RETRY

If the step declares `verifyMode: RETRY`, the engine wraps your statement block in a polling loop (via `Vouchfx.Engine.Abstractions.Retry.RetryRunner`). Your block runs unchanged, multiple times, until it passes or `timeout` elapses.

**You do not implement polling yourself.** Write a re-runnable block that:
- Writes a verdict on every invocation
- Uses `Fail` (never `Inconclusive`) for "not yet satisfied" assertions
- The engine's `RetryRunner` converts a sustained `Fail` into `Inconclusive` once the timeout window elapses

This means your block must be idempotent — it should produce consistent results when run multiple times against the same system state.

## Placeholders and Secrets

YAML fields may contain template references resolved at step-execution time:
- `{placeholder}` — replaced from `Vars`
- `${secret:source/path}` — replaced from the secrets subsystem

Your provider must resolve **every string field** the author might write (url, method, params values, headers, etc.). Resolution happens **inside the emitted CSX, at step-execution time** — never in your `Bind`/`Validate`/`Emit` methods, which run at compile time before any secret source is even available. Both kinds of token are resolved together, in a single left-to-right pass, by a helper class your provider splices into the script.

### The `Secret_Helpers` Prerequisite

`Secret_Helpers.ResolveTemplate` only exists inside the emitted script because your provider adds its canonical source, `Vouchfx.Sdk.SecretHelper.Source`, to `CsxFragment.RequiredHelpers` — exactly as the JSON-RPC provider does:

```csharp
return new CsxFragment(
    RequiredUsings: s_usings,
    RequiredHelpers: new[] { HelperSource, SecretHelper.Source },
    StatementBlock: block);
```

`SecretHelper.Source` is byte-identical for every provider that includes it, so the assembler deduplicates it to one copy per suite — never write your own copy of this helper.

### Resolving inside the emitted CSX

Inside your `RequiredHelpers` method or `StatementBlock` — not in provider C# — resolve a template field with:

```csharp
var url = Secret_Helpers.ResolveTemplate(secrets, vars, urlTemplate);
```

This is the real signature (`Vouchfx.Sdk/SecretHelper.cs`): `internal static string ResolveTemplate(ISecretAccessor secrets, IDictionary<string, object?> vars, string template)` — **synchronous**, argument order `(secrets, vars, template)`. It resolves `${secret:source/path}` tokens and `{placeholder}` tokens in a single pass over the *original* text, so a substituted placeholder value is never re-scanned for secret tokens and a revealed secret value is never re-scanned for placeholders. Call it inside your own guarded `try` region: a missing or unknown secret throws `SecretResolutionException`, which your `catch` must map to `Verdict.EnvironmentError`.

**Important:** the revealed value is a transient destined for an injection sink — it is consumed immediately and **is never written back to `Vars`**. Resolved values must never reach observations, exceptions, or logs:

```csharp
// ❌ WRONG — the resolved secret leaks into the observation
observation = "{\"password\":\"" + resolvedSecret + "\"}";

// ✅ CORRECT — record only the exception's type name, never its message or the value
catch (System.Exception ex)
{
    observation = "{\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.GetType().Name) + "}";
}
```

`SecretString` (`Vouchfx.Engine.Abstractions.Secrets.SecretString`) exists for the engine's own secrets subsystem to carry a resolved value with redaction enforced at the source (`ToString()` returns a fixed `***REDACTED***` marker, never the value) — its constructor is `internal`, so neither your provider nor your emitted CSX can construct one. Do not try to cache a revealed value in `Vars` for later reuse: if a later part of the same statement block needs the value again, call `ResolveTemplate` again on its template.

**Walk string leaves, not raw JSON:** when a field like `params` is a JSON string, parse it into a tree, resolve each string leaf individually via `Secret_Helpers.ResolveTemplate`, then re-serialise — never template-substitute the raw JSON text (a resolved value containing a quote or brace would corrupt the structure):

```csharp
// Inside RequiredHelpers, mirroring the JSON-RPC provider pattern
private static void ResolveParamsLeaves(
    System.Text.Json.Nodes.JsonNode? node,
    Vouchfx.Engine.Abstractions.Secrets.ISecretAccessor secrets,
    System.Collections.Generic.IDictionary<string, object?> vars)
{
    if (node is System.Text.Json.Nodes.JsonObject obj)
    {
        var keys = new System.Collections.Generic.List<string>();
        foreach (var kv in obj)
            keys.Add(kv.Key);

        foreach (var key in keys)
        {
            var child = obj[key];
            if (child is System.Text.Json.Nodes.JsonValue leaf && leaf.TryGetValue<string>(out var s))
                obj[key] = System.Text.Json.Nodes.JsonValue.Create(Secret_Helpers.ResolveTemplate(secrets, vars, s));
            else
                ResolveParamsLeaves(child, secrets, vars);
        }
    }
    // A JsonArray branch mirrors the object branch above for complete coverage.
}
```

## Capture — JSONPath Evaluation into Vars

The engine's universal `capture` field lets the test author extract values from your step's response into `Vars` for later steps to use. There is no shared runtime facility that evaluates captures for you: your provider's `Emit` method reads the declared captures from `ICompileContext.CaptureExprs` — an `IReadOnlyDictionary<string, CaptureExpr>`, where each `CaptureExpr` carries the extractor `Format` (`CaptureFormat.JsonPath` or `CaptureFormat.XPath`) and the raw `Expression` string — and your provider must itself emit the CSX that evaluates those expressions against the response at step-execution time and downgrades an unmet capture to `Inconclusive`.

The two phases run at different times and must not be conflated: `ctx.CaptureExprs` is only in scope inside `Emit` (compile time); the response only exists once the step runs (runtime).

**Emit-time — in `Emit(model, ctx)`, turn the capture map into literal arrays and pass them as arguments:**

```csharp
var captureVarNames = ctx.CaptureExprs.Keys.ToArray();
var captureExprs = ctx.CaptureExprs.Values.Select(c => c.Expression).ToArray();

var block = $$"""
    {
        await MyKind_Helpers.ExecuteAsync(
            Vars,
            {{JsonSerializer.Serialize(safeId)}},
            {{BuildStringArrayLiteral(captureVarNames)}},
            {{BuildStringArrayLiteral(captureExprs)}});
    }
    """;
```

(`BuildStringArrayLiteral` is the small emit-time helper — JSON-serialising each element into a C# array-initialiser literal.)

**Runtime — inside your `RequiredHelpers` method, evaluate each expression against the parsed response:**

```csharp
for (int ci = 0; ci < captureVarNames.Length; ci++)
{
    var matched = false;
    try
    {
        var pathResult = Json.Path.JsonPath.Parse(captureExprs[ci]).Evaluate(responseNode);
        var matches = pathResult.Matches;
        if (matches is not null && matches.Count > 0 && matches[0].Value is not null)
        {
            vars[captureVarNames[ci]] = matches[0].Value!.ToJsonString();
            matched = true;
        }
    }
    catch (System.Exception) { matched = false; }

    if (!matched)
    {
        // An unmet capture downgrades an otherwise-Pass verdict to Inconclusive.
        verdict = Vouchfx.Engine.Abstractions.Verdict.Inconclusive;
        observation = "{\"captureUnmet\":" + System.Text.Json.JsonSerializer.Serialize(captureVarNames[ci]) + "}";
    }
}
```

The illustrative observation above is JSON:

```json
{ "captureUnmet": "total" }
```

Your provider must implement this evaluate-and-downgrade pattern itself. See the Core `http.rest` provider, or the JSON-RPC provider, for the full worked example — note that `Json.Path.JsonPath` must be declared via `ICompileReferenceContributor`, since it is not in the engine's minimal default script reference set.

---

**Next:** [Testing Your Provider](provider-testing.md) — conformance tests, harnesses and Docker integration tests.
