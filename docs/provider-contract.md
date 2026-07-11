# The Contract Surfaces

**Stage 3 of the provider authoring journey**

Your provider class implements four mandatory `Vouchfx.Sdk` interfaces: `IStepProvider`, `IStepBinder<TModel>`, `IStepValidator<TModel>`, and `IStepCompiler<TModel>`. Beyond those, the SDK exposes six further **optional** extension interfaces that a provider implements only when it needs the capability: `ICompileReferenceContributor`, `IResourceContributor<TModel>`, `IHostResourceContributor<TModel>`, `IRuntimeServiceContributor`, `IStepDiffRenderer`, and `IProviderModule`. Adding any of these to a provider does not change the frozen v1 contract — they are additive-only extensions. This guide details three of them below: `ICompileReferenceContributor`, `IResourceContributor<TModel>`, and `IStepDiffRenderer`.

## Mandatory Interface 1: `IStepProvider`

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

## Mandatory Interface 2: `IStepBinder<TModel>`

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

## Mandatory Interface 3: `IStepValidator<TModel>`

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

If you need to validate something involving `capture`, defer that check to `Emit` time and produce a fail-safe block (see [Verdicts section](provider-csx-composition.md)).

## Mandatory Interface 4: `IStepCompiler<TModel>`

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
            var __value_{{safeId}} = {{JsonSerializer.Serialize(model.Method)}};
            __sw_{{safeId}}.Stop();

            var __verdict_{{safeId}} =
                MyKind_Helpers.Check(__value_{{safeId}}, "expected")
                    ? Vouchfx.Engine.Abstractions.Verdict.Pass
                    : Vouchfx.Engine.Abstractions.Verdict.Fail;

            Vars[Vouchfx.Engine.Abstractions.VarKeys.Outcome("{{safeId}}")] =
                new Vouchfx.Engine.Abstractions.StepOutcome(
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
- The verdict is one of the four taxonomy outcomes (see [Verdicts](provider-csx-composition.md))

## Optional Interface: `ICompileReferenceContributor`

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

The JsonRpc provider implements this because its emitted code calls `HttpClient`, `JsonNode`, and `JsonPath.Net`.

## Optional Interface: `IResourceContributor<TModel>`

Declare which Aspire-managed infrastructure dependency your provider needs. The engine reconciles your declarations against `environment.dependencies` before starting the topology:

```csharp
public IEnumerable<ResourceRequirement> Resources(MyKindModel model)
{
    // This step targets the dependency named "my-db", declared under
    // environment.dependencies as `type: postgres`. Family must name one of
    // the engine's managed dependency types (see the Overview's Infrastructure Providers list);
    // Image is conventionally left null today, so the engine uses its own
    // default image for that family (see EnvironmentMapper's dependency
    // registry, in the engine repository).
    yield return new ResourceRequirement(
        Family: "postgres",
        Name: "my-db",
        Image: null);
}
```

`ResourceRequirement` is a plain record: `ResourceRequirement(string Family, string Name, string? Image)`. This interface only lets your provider *observe* a dependency type the engine's orchestration already manages — it cannot introduce a brand-new dependency type; that requires an engine-side change.

**When to implement:** Only if your provider needs Aspire-managed infrastructure (databases, brokers, etc.). Protocol providers (talking to URLs) do not implement this.

## Optional Interface: `IStepDiffRenderer`

Render an expected-vs-observed diff for display in terminals and HTML reports, computed by the renderer at *render time* — never by the engine when it records the event, which keeps the schema-versioned JSON Lines event stream pure structured data. Implement this if your provider produces observation objects that have a natural "expected" and "actual" shape. The interface has two members, both over `System.Text.Json.JsonElement`:

```csharp
public bool CanRender(System.Text.Json.JsonElement observation) =>
    TryReadResultMismatch(observation, out _, out _, out _);

public string? RenderDiff(System.Text.Json.JsonElement observation)
{
    if (TryReadResultMismatch(observation, out var path, out var expected, out var actual))
        return $"  path     {path}{Environment.NewLine}  expected {expected}{Environment.NewLine}  actual   {actual}";

    return null;
}
```

`CanRender` returns `true` only when `RenderDiff` would return a non-null diff for that same observation shape; a renderer that does not recognise the shape returns `false`/`null` and the caller falls back to the plain verdict line. Mirrors the JSON-RPC provider's own observation shapes, which recognises its own `{"resultMismatch": {...}}` and `{"errorCodeMismatch": {...}}` Fail-observation shapes.

This is rarely needed. Only implement it if your step produces structured assertions where a diff makes sense (e.g., "expected 5, got 7").

---

**Next:** [CSX Composition](provider-csx-composition.md) — the Roslyn composition rules, verdicts, secrets and capture.
