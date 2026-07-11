# Testing Your Provider

**Stage 5 of the provider authoring journey**

Testing proves your provider works end-to-end through the full engine pipeline.

## Conformance Tests: `ProviderTestHarness`

For dependency-free providers (no infrastructure), use the published `ProviderTestHarness.RunSingleStepAsync()`:

```csharp
using Vouchfx.Sdk.Testing;
using Xunit;

namespace Vouchfx.Community.Hello.Tests;

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

## Custom Harness: When Your Provider Needs Extra References

If your provider implements `ICompileReferenceContributor` (because your emitted code calls `HttpClient`, `JsonPath.Net`, etc.), `ProviderTestHarness` will fail compilation — it does not include your contributed references.

Use the custom-harness pattern (modelled on the engine's own test fixtures), as demonstrated by the JSON-RPC provider (`community/Vouchfx.Community.JsonRpc.Tests/JsonRpcHarness.cs`):

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

## Unit Tests: CSX Composition

Test that your `Emit` produces a well-formed `CsxFragment`:

```csharp
[Fact]
public void Unit_Emit_FragmentSatisfiesCompositionRules()
{
    var provider = new MyKindProvider();
    var model = new MyKindModel(Url: "https://example.test/api", Method: "ping");
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
    var model = new MyKindModel(Url: "https://example.test/api", Method: "ping");
    var ctx = new TestCompileContext(stepId: "my-step-id");

    var fragment = provider.Emit(model, ctx);

    // The block must use the sanitised id (hyphens → underscores)
    Assert.Contains("my_step_id", fragment.StatementBlock);
    Assert.DoesNotContain("my-step-id", fragment.StatementBlock);
}
```

## Docker Integration Tests

For infrastructure providers, create integration tests that start a real Docker container and drive it through your provider. `Vouchfx.Sdk` does not publish a container-lifecycle API of its own — the engine's own Docker-gated fixtures start containers through its internal orchestration, which is engine-internal and not part of the published SDK surface your provider package can reference. Outside the engine repository, use whichever container-testing library you prefer (for example, Testcontainers for .NET) to start the container, then exercise your provider through the harness exactly as the conformance tests do:

```csharp
[Collection("Docker")]  // Serialise Docker tests
public sealed class MyKindProviderIntegrationTests : IAsyncLifetime
{
    // Container start-up/teardown is your own choice of tooling — not
    // prescribed by Vouchfx.Sdk. Whatever you use, expose the resulting
    // base URL so the model below can target it.
    private string? _baseUrl;

    public async Task InitializeAsync()
    {
        _baseUrl = await StartMyKindContainerAsync();
    }

    public Task DisposeAsync() => StopMyKindContainerAsync();

    [Fact]
    public async Task Integration_MyKindStep_ConnectsAndAsserts()
    {
        var model = new MyKindModel(Url: _baseUrl!, Method: "ping");
        var result = await MyKindHarness.RunAsync(model, "test-step");

        Assert.Equal(Verdict.Pass, result.Verdict);
    }
}
```

## Running Tests Locally

```bash
# All tests (including Docker)
dotnet test your-provider.Tests

# Only non-Docker tests (for CI-free iteration)
dotnet test your-provider.Tests -c Release --filter "requires!=docker"
```

---

**Next:** [Publishing & Submission](provider-publishing.md) — the Community listing paths, hub hosting, and the Vouched badge.
