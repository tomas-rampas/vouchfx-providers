// Community.Steps.JsonRpc.Tests — JsonRpcHarness.
//
// WHY THIS EXISTS (a documented deviation from the brief's default expectation that
// every conformance test drives Platform.Sdk.Testing.ProviderTestHarness.RunSingleStepAsync):
//
// ProviderTestHarness.RunSingleStepAsync explicitly does NOT run the engine's
// IResourceContributor / IHostResourceContributor / ICompileReferenceContributor
// contributor stages (see its own XML doc remarks, "Scope — what this single-step
// path runs and what it does not", in Platform.Sdk.Testing in the engine repo). It
// calls RoslynScriptCompiler.CompileOnce(assembled.CsxSource) with NO
// additionalReferencePaths. RoslynScriptCompiler's own default reference set (see
// RoslynScriptCompiler.BuildTpaReferences in the engine repo) is deliberately minimal:
// System.Private.CoreLib, System.Runtime, System.Collections, and
// System.Text.RegularExpressions — NOT System.Net.Http, NOT System.Text.Json, and NOT
// any third-party package such as JsonPath.Net.
//
// rpc.json-rpc's emitted CSX genuinely needs HttpClient, System.Text.Json.Nodes, and
// JsonPath.Net at Roslyn compile time (exactly like the engine's own Core http.rest /
// mail-expect.smtp providers — see JsonRpcProvider's ICompileReferenceContributor).
// Driven through the plain ProviderTestHarness, every execution test below would throw
// a Platform.Engine.Compilation.ScriptCompilationException (CS0246, type not found) —
// not a soft Verdict. This is a genuine, cited SDK/tooling gap this provider surfaces,
// not a guess: the engine's OWN docker-free test for http.rest
// (tests/Platform.Engine.Compilation.Tests/HttpRestExecutionTests.cs) works around the
// identical limitation by driving Emit -> CsxAssembler.Assemble ->
// RoslynScriptCompiler.CompileOnce(..., additionalReferencePaths: ...) ->
// RunIsolatedAsync directly, bypassing ProviderTestHarness entirely. This harness
// mirrors that exact, already-proven-in-the-engine pattern, using only PUBLICLY
// published packages (Platform.Sdk.Testing's own transitive dependencies on
// Platform.Engine.Abstractions / Authoring / Compilation) — no engine repo changes,
// no reflection hacks.
//
// It adds two things ProviderTestHarness does not offer at all:
//   1. additionalReferencePaths, sourced directly from the provider's own
//      ICompileReferenceContributor (so this harness can never drift from what the
//      real engine would supply via that contributor stage).
//   2. Vars exposure after the run, so a test can assert a `capture` actually wrote a
//      variable — ProviderTestHarness's StepRunResult carries only the single step's
//      Verdict/Observation/DurationMs (its own `vars` dictionary is a method-local that
//      is never returned; verified by reading ProviderTestHarness.cs directly).
//
// Schema-rejection and model-validation-rejection tests do NOT need any of this (they
// halt BEFORE Roslyn ever compiles anything) and use the plain, published
// ProviderTestHarness.RunSingleStepAsync instead — see
// JsonRpcProviderTests.Conformance_MissingUrlAndMethod_FailsSchemaValidation.
using Platform.Engine.Abstractions;
using Platform.Engine.Abstractions.Secrets;
using Platform.Engine.Compilation;
using Platform.Sdk;
using Platform.Sdk.Testing.Contexts;

namespace Community.Steps.JsonRpc.Tests;

/// <summary>
/// Drives <see cref="JsonRpcProvider"/> end to end (Validate -&gt; Emit -&gt; assemble
/// -&gt; compile-once-with-extra-references -&gt; run-isolated) from a hand-built
/// <see cref="JsonRpcModel"/>, without going through YAML/schema/bind — mirroring the
/// engine's own <c>HttpRestExecutionTests</c> pattern for an HTTP-calling provider.
/// </summary>
internal static class JsonRpcHarness
{
    /// <summary>
    /// The compile-time metadata references <see cref="JsonRpcProvider"/> itself
    /// declares via <see cref="ICompileReferenceContributor"/> — reused here instead of
    /// a hand-duplicated list so this harness can never drift from what the real
    /// engine pipeline would supply via that contributor stage.
    /// </summary>
    private static readonly IReadOnlyList<string> AdditionalReferencePaths =
        new JsonRpcProvider().CompileReferenceAssemblies
            .Select(a => a.Location)
            .ToArray();

    /// <summary>The outcome of driving one step through this harness.</summary>
    public sealed record Result(
        Verdict Verdict,
        string? Observation,
        long DurationMs,
        IReadOnlyDictionary<string, object?> Vars);

    /// <summary>
    /// Runs <paramref name="model"/> as a single step named <paramref name="stepId"/>.
    /// </summary>
    /// <param name="model">The step model to compile and execute.</param>
    /// <param name="stepId">The step id (sanitised internally before use in Vars keys).</param>
    /// <param name="retry">
    /// <see langword="true"/> to wrap the emitted block in the engine-owned
    /// <c>verifyMode: RETRY</c> polling loop, exactly as
    /// <c>Platform.Engine.Runtime.ProviderPipeline</c> / the published
    /// <c>ProviderTestHarness</c> do — see <c>StepCompilePlan.Retry</c>.
    /// </param>
    /// <param name="timeoutMs">
    /// The RETRY polling window, or <see langword="null"/> for the
    /// <c>RetryRunner</c> engine default. Ignored when <paramref name="retry"/> is
    /// <see langword="false"/>. Keep this small in tests so a RETRY-exhausted case
    /// stays fast.
    /// </param>
    /// <param name="captures">
    /// The step's <c>capture</c> map, or <see langword="null"/> for none.
    /// </param>
    /// <param name="preSeedVars">
    /// Variables to seed into <c>Vars</c> BEFORE the step runs — mirrors how the real
    /// engine pipeline pre-loads the scenario's top-level <c>variables:</c> section
    /// (DSL §3) before any step executes. <see langword="null"/> (the default) seeds
    /// nothing, matching every pre-existing caller of this method.
    /// </param>
    /// <param name="secrets">
    /// The <see cref="ISecretAccessor"/> to expose via <c>ScriptGlobalVariables.Secrets</c>,
    /// or <see langword="null"/> (the default) to fall back to the legacy
    /// <see cref="ScriptGlobalVariables(IDictionary{string, object?})"/> constructor
    /// (a <c>NullSecretAccessor</c> that throws on any resolution attempt) — matching
    /// every pre-existing caller of this method exactly.
    /// </param>
    public static async Task<Result> RunAsync(
        JsonRpcModel model,
        string stepId,
        bool retry = false,
        long? timeoutMs = null,
        IReadOnlyDictionary<string, CaptureExpr>? captures = null,
        IReadOnlyDictionary<string, object?>? preSeedVars = null,
        ISecretAccessor? secrets = null,
        CancellationToken cancellationToken = default)
    {
        var provider = new JsonRpcProvider();

        var validation = provider.Validate(model, new TestProjectContext());
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"JsonRpcHarness.RunAsync was given an invalid model for step '{stepId}': " +
                string.Join("; ", validation.Errors));
        }

        var compileCtx = new TestCompileContext(stepId, captureExprs: captures);
        var fragment = provider.Emit(model, compileCtx);

        var plan = new StepCompilePlan(
            StepId: stepId,
            Fragment: fragment,
            Retry: retry,
            TimeoutMs: timeoutMs,
            PollIntervalMs: null);

        var assembled = CsxAssembler.Assemble(new[] { plan });
        var compiled = RoslynScriptCompiler.CompileOnce(
            assembled.CsxSource,
            additionalReferencePaths: AdditionalReferencePaths);

        var vars = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (preSeedVars is not null)
        {
            foreach (var (key, value) in preSeedVars)
                vars[key] = value;
        }

        var globals = secrets is null
            ? new ScriptGlobalVariables(vars)
            : new ScriptGlobalVariables(vars, new Dictionary<string, object>(StringComparer.Ordinal), secrets);

        await RoslynScriptCompiler
            .RunIsolatedAsync(compiled, globals, runLabel: stepId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var safeId = CsxFragment.SanitiseId(stepId);
        var outcomeKey = VarKeys.Outcome(safeId);

        if (!vars.TryGetValue(outcomeKey, out var raw) || raw is not StepOutcome outcome)
        {
            throw new InvalidOperationException(
                $"The step '{stepId}' did not write a {nameof(StepOutcome)} under '{outcomeKey}'. " +
                $"Keys present: [{string.Join(", ", vars.Keys)}].");
        }

        return new Result(outcome.Verdict, outcome.Observation, outcome.DurationMs, vars);
    }
}
