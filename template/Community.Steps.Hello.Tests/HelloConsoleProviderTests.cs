// Community.Steps.Hello.Tests — conformance + unit tests for the template provider.
//
// TWO test categories:
//
//   1. CONFORMANCE (the CI merge gate)
//      Uses ProviderTestHarness.RunSingleStepAsync to drive the provider through
//      the full published engine pipeline (schema-validate → parse → bind → validate
//      → emit → assemble → compile-once → run-isolated) WITHOUT Docker.  This is the
//      pattern every Verified-tier submission must replicate.
//
//   2. UNIT (emit surface)
//      Uses TestCompileContext to call the provider's Emit directly and assert the
//      CsxFragment is well-formed per §13.3.1 composition rules.
//
// Both tests are dependency-free (no containers, no Aspire topology) — `hello.console`
// is a pure computation step.
//
// BDD: the conformance test was written FIRST (RED — the stub Emit wrote no outcome),
// then the real Emit was implemented to make it pass (GREEN).

using Community.Steps.Hello;
using Platform.Engine.Abstractions;
using Platform.Sdk;
using Platform.Sdk.Testing;
using Platform.Sdk.Testing.Contexts;
using Xunit;

namespace Community.Steps.Hello.Tests;

/// <summary>
/// Conformance and unit tests for the <c>hello.console</c> template provider.
/// </summary>
public sealed class HelloConsoleProviderTests
{
    // ── Conformance test (the CI merge gate) ─────────────────────────────────

    /// <summary>
    /// End-to-end conformance gate: a <c>hello.console</c> step with a matching
    /// <c>expect</c> value runs through the full published engine pipeline and
    /// resolves to <see cref="Platform.Engine.Abstractions.Verdict.Pass"/>.
    /// </summary>
    /// <remarks>
    /// This test is the CI merge gate for the template provider — and the pattern
    /// every Verified-tier provider submission must replicate with its own step kind.
    /// It proves:
    /// <list type="bullet">
    ///   <item>The <c>[StepProvider]</c> attribute is present and the registry discovers the type.</item>
    ///   <item>The <c>JsonSchemaFragment</c> composes into the language schema, so the YAML passes validation.</item>
    ///   <item><c>Bind</c> produces a valid model; <c>Validate</c> accepts it.</item>
    ///   <item><c>Emit</c> produces a <see cref="CsxFragment"/> that Roslyn compiles without error.</item>
    ///   <item>The emitted block runs in an isolated collectible context and writes a <c>StepOutcome</c>
    ///         under <c>Vars[VarKeys.Outcome(...)]</c> with verdict Pass.</item>
    /// </list>
    /// </remarks>
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
            "say-hello");

        Assert.Empty(result.SchemaErrors);
        Assert.Empty(result.ValidationErrors);
        Assert.True(
            result.IsPass,
            $"Expected Pass but Verdict={result.Verdict}. Observation: {result.Observation}");
    }

    /// <summary>
    /// Fail path: a <c>hello.console</c> step whose <c>expect</c> does not match
    /// the <c>message</c> resolves to <c>Fail</c> — not an exception — proving the
    /// assertion in <c>Emit</c> is real and the Pass above is meaningful.
    /// </summary>
    [Fact]
    public async Task Conformance_HelloConsoleStep_MismatchedExpect_RunsEndToEnd_Fail()
    {
        const string yaml = """
            steps:
              - id: say-hello
                type: hello.console
                message: "hello, community"
                expect: "goodbye, community"
            """;

        var result = await ProviderTestHarness.RunSingleStepAsync(
            yaml,
            typeof(HelloConsoleProvider).Assembly,
            "say-hello");

        Assert.Empty(result.SchemaErrors);
        Assert.Empty(result.ValidationErrors);
        Assert.Equal(Verdict.Fail, result.Verdict);
    }

    /// <summary>
    /// Schema gate: a step that omits the required <c>message</c> field must fail
    /// schema validation before reaching the provider — proving the
    /// <see cref="JsonSchemaFragment"/> is composed into the language schema and enforced.
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
            "say-hello");

        Assert.NotEmpty(result.SchemaErrors);
        Assert.Null(result.Verdict);
    }

    // ── Unit test (emit surface) ──────────────────────────────────────────────

    /// <summary>
    /// Unit test for <c>Emit</c>: the produced <see cref="CsxFragment"/> satisfies
    /// the §13.3.1 composition rules — correct usings, provider-id-prefixed helper,
    /// and a brace-enclosed statement block that writes the outcome.
    /// </summary>
    [Fact]
    public void Unit_Emit_FragmentSatisfiesCompositionRules()
    {
        var provider = new HelloConsoleProvider();
        var model = new HelloConsoleModel(Message: "hello", Expected: "hello");
        var ctx = new TestCompileContext(stepId: "say-hello");

        var fragment = provider.Emit(model, ctx);

        // RequiredUsings: bare namespace strings only (§13.3.1).
        Assert.Contains("System", fragment.RequiredUsings);
        Assert.Contains("Platform.Engine.Abstractions", fragment.RequiredUsings);
        // No inline `using` keyword — the engine emits those.
        Assert.All(fragment.RequiredUsings, u => Assert.False(
            u.TrimStart().StartsWith("using ", StringComparison.Ordinal),
            $"RequiredUsings must be bare namespace strings, got: '{u}'"));

        // RequiredHelpers: provider-id-prefixed helper class (§13.3.1).
        Assert.NotEmpty(fragment.RequiredHelpers);
        Assert.Contains(
            fragment.RequiredHelpers,
            h => h.Contains("HelloConsole_Helpers", StringComparison.Ordinal));

        // StatementBlock: brace-enclosed block that writes the outcome key (§13.3.1).
        var block = fragment.StatementBlock;
        Assert.StartsWith("{", block.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("}", block.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("VarKeys.Outcome", block, StringComparison.Ordinal);

        // No `using var` in the body (illegal in Roslyn script bodies, §13.3.1).
        Assert.DoesNotContain("using var", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// Unit test: step id hyphens are sanitised to underscores before being spliced
    /// into the emitted variable names (§13.3.1 <c>CsxFragment.SanitiseId</c> rule).
    /// </summary>
    [Fact]
    public void Unit_Emit_HyphenatedStepId_SanitisedInBlock()
    {
        var provider = new HelloConsoleProvider();
        var model = new HelloConsoleModel(Message: "hi", Expected: "hi");
        var ctx = new TestCompileContext(stepId: "my-step-id");

        var fragment = provider.Emit(model, ctx);

        // The block must contain the sanitised id (hyphens → underscores), not the raw one.
        Assert.Contains("my_step_id", fragment.StatementBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("my-step-id", fragment.StatementBlock, StringComparison.Ordinal);
    }
}
