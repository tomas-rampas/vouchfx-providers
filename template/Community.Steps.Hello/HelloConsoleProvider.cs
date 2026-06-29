// ─────────────────────────────────────────────────────────────────────────────
// COPYABLE TEMPLATE — Community provider starter (S12-F-01).
//
// `hello.console` is the smallest meaningful step kind: it emits a message and
// asserts it equals a constant.  It has NO infrastructure dependency, so the
// conformance test runs it end-to-end WITHOUT Docker.  Copy this project to
// bootstrap your own provider; the four steps a contributor takes are exactly:
//
//   1. Add a project that references ONLY Platform.Sdk (this csproj).
//   2. Define a strongly-typed model record : IStepModel (HelloConsoleModel).
//   3. Implement the v1 contract on one [StepProvider]-decorated class (this file):
//        IStepProvider + IStepBinder<T> + IStepValidator<T> + IStepCompiler<T>.
//   4. The reflective StepKindRegistry discovers it at startup — no registration code.
//
// When you copy this template, rename:
//   • The namespace (Community.Steps.Hello → YourOrg.Steps.YourKind).
//   • The step kind ("hello" / "console" → your family / provider).
//   • The model record and its fields.
//   • The helper class prefix (HelloConsole_ → YourKind_).
//
// §5.6 ASSEMBLY-GRAPH HYGIENE: use a NON-reserved namespace.
//   The `Platform.Steps.*` and `Platform.Engine.*` namespaces are RESERVED for the
//   engine and its Core providers — a customer DLL declaring them is refused at
//   startup.  This template uses `Community.Steps.Hello` to model the rule.
//
// §5 MEMORY MODEL: this provider takes NO reference to Platform.Engine.Abstractions.
//   The emitted CSX reaches the run environment ONLY through the engine-injected
//   `Vars` global and refers to engine types by fully-qualified name — the engine
//   already references Platform.Engine.Abstractions when it compiles the assembled
//   script, so no static handle from this provider bridges the collectible ALC.
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using Platform.Sdk;
using YamlDotNet.RepresentationModel;

namespace Community.Steps.Hello;

/// <summary>
/// Template provider for the <c>hello.console</c> step kind — a trivial,
/// dependency-free step that emits a greeting and asserts it equals a constant.
/// </summary>
/// <remarks>
/// <para>
/// This class is a copyable template for authoring a non-Core provider against
/// the frozen v1 <see cref="Platform.Sdk"/> contract (§13).  It implements the
/// four mandatory provider interfaces on a single <c>[StepProvider]</c>-decorated
/// class: <see cref="IStepProvider"/> (identity), <see cref="IStepBinder{TModel}"/>
/// (YAML → model + schema fragment), <see cref="IStepValidator{TModel}"/> (model
/// rules), and <see cref="IStepCompiler{TModel}"/> (model → <see cref="CsxFragment"/>).
/// It does <em>not</em> implement the optional <see cref="IResourceContributor{TModel}"/>
/// because the step needs no infrastructure — that omission is exactly why the
/// conformance test runs without Docker.
/// </para>
/// <para>
/// <strong>CsxFragment composition rules observed (§13.3.1):</strong>
/// <list type="bullet">
///   <item><see cref="CsxFragment.RequiredUsings"/> — bare namespace strings only.</item>
///   <item><see cref="CsxFragment.RequiredHelpers"/> — one nested <c>static</c> class
///         whose name is PREFIXED with the provider id (<c>HelloConsole_Helpers</c>).
///         The engine de-duplicates by declared class name; the helper source must be
///         byte-identical across all steps of the same kind in one suite.</item>
///   <item><see cref="CsxFragment.StatementBlock"/> — exactly one brace-enclosed block,
///         built as a C# 11 double-dollar raw string (<c>$$"""…"""</c>): a single brace
///         is a literal brace in the emitted CSX, and <c>{{hole}}</c> is an interpolation
///         hole the emitter fills at emit time.</item>
///   <item>No <c>using var</c> (illegal in a Roslyn script body).</item>
///   <item>Step ids sanitised via <see cref="CsxFragment.SanitiseId"/> before splicing.</item>
///   <item>Cross-step state passes ONLY through the <c>Vars</c> global.</item>
/// </list>
/// </para>
/// </remarks>
[StepProvider]
public sealed class HelloConsoleProvider
    : IStepProvider,
      IStepBinder<HelloConsoleModel>,
      IStepValidator<HelloConsoleModel>,
      IStepCompiler<HelloConsoleModel>
{
    // Bare namespace strings — the engine emits the `using` lines and de-duplicates
    // across all providers (§13.3.1).  The emitted body depends ONLY on types the
    // engine's Roslyn base reference set always provides (System.Private.CoreLib /
    // System.Runtime / Platform.Engine.Abstractions): a portable provider must not
    // assume references the minimal compile path does not guarantee.
    private static readonly IReadOnlyList<string> s_usings = new[]
    {
        "System",
        "System.Diagnostics",
        "Platform.Engine.Abstractions",
    };

    // ── IStepProvider ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public StepKindId Kind { get; } = new StepKindId("hello", "console");

    /// <inheritdoc />
    public ProviderMetadata Metadata { get; } = new ProviderMetadata(
        Version: "1.0.0",
        MinEngineVersion: "1.0.0",
        License: "Apache-2.0",
        Authors: new[] { "vouchfx-community" });

    // ── IStepBinder<HelloConsoleModel> ────────────────────────────────────────

    /// <summary>
    /// Gets the JSON Schema fragment describing the <c>hello.console</c> provider's own
    /// fields (<c>message</c>, <c>expect</c>).
    /// </summary>
    /// <remarks>
    /// The fragment does NOT declare the <c>type</c> discriminator — the engine's
    /// <c>SchemaComposer</c> derives that from <see cref="Kind"/> and injects it as
    /// an <c>if</c>/<c>then</c> clause (§13.6).
    /// </remarks>
    public JsonSchemaFragment SchemaFragment { get; } = new JsonSchemaFragment(
        """
        {
          "type": "object",
          "required": ["message"],
          "properties": {
            "message": {
              "description": "The greeting the step emits at execution time.",
              "type": "string"
            },
            "expect": {
              "description": "The constant the emitted message is asserted to equal.  Defaults to 'message' when omitted (a bare emit always passes).",
              "type": "string"
            }
          },
          "additionalProperties": true
        }
        """);

    /// <inheritdoc />
    /// <remarks>
    /// BIND → VALIDATE CONTRACT (§13): Bind only shapes YAML into the model; it does
    /// not reject input.  A non-mapping node or missing field yields an empty model,
    /// which the Validate stage then catches with a clear diagnostic.  Never move
    /// rejection logic here.
    /// </remarks>
    public HelloConsoleModel Bind(YamlNode node, IBindingContext ctx)
    {
        if (node is not YamlMappingNode mapping)
            return new HelloConsoleModel(Message: string.Empty, Expected: string.Empty);

        var message = ReadScalar(mapping, "message") ?? string.Empty;

        // `expect` is optional; default it to `message` so a step that only emits
        // (and asserts nothing meaningful) always passes.
        var expected = ReadScalar(mapping, "expect") ?? message;

        return new HelloConsoleModel(Message: message, Expected: expected);
    }

    private static string? ReadScalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var value)
        && value is YamlScalarNode scalar
            ? scalar.Value
            : null;

    // ── IStepValidator<HelloConsoleModel> ─────────────────────────────────────

    /// <inheritdoc />
    public ValidationResult Validate(HelloConsoleModel model, IProjectContext ctx)
    {
        if (string.IsNullOrWhiteSpace(model.Message))
        {
            return ValidationResult.Failure(
                "hello.console: 'message' must not be empty or whitespace.");
        }

        return ValidationResult.Success;
    }

    // ── IStepCompiler<HelloConsoleModel> ──────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Emits a single statement block that:
    /// <list type="number">
    ///   <item>starts a <c>Stopwatch</c>;</item>
    ///   <item>compares the (interpolated, JSON-escaped) message against the expectation
    ///         via the provider-prefixed helper <c>HelloConsole_Helpers.Check</c>;</item>
    ///   <item>writes a <c>StepOutcome</c> (Pass/Fail + duration + observation) to
    ///         <c>Vars</c> under the engine's canonical outcome key.</item>
    /// </list>
    /// </remarks>
    public CsxFragment Emit(HelloConsoleModel model, ICompileContext ctx)
    {
        // Hyphens are legal in YAML step ids but illegal in C# identifiers — sanitise
        // before splicing into emitted variable names (§13.3.1).
        var safeId = CsxFragment.SanitiseId(ctx.StepId);

        // Emit author-supplied text as JSON-escaped C# string literals (defence against
        // quotes / braces / backslashes corrupting the emitted source).
        var messageLiteral = JsonSerializer.Serialize(model.Message);
        var expectedLiteral = JsonSerializer.Serialize(model.Expected);

        // Build the structured observation at emit time (where System.Text.Json is
        // available) and splice it in as a single safe string literal, so the emitted
        // CSX needs no JSON reference at runtime.
        var observationJson =
            "{\"message\":" + messageLiteral + ",\"expected\":" + expectedLiteral + "}";
        var observationLiteral = JsonSerializer.Serialize(observationJson);

        // Provider-id-prefixed nested static helper (§13.3.1).  The engine de-duplicates
        // helpers by declared class name; every hello.console step in a suite must emit
        // byte-IDENTICAL helper source — so the helper carries NO step-specific data.
        const string helper = """
            static class HelloConsole_Helpers
            {
                // Returns true when the emitted message matches the expectation.
                public static bool Check(string message, string expected)
                    => string.Equals(message, expected, System.StringComparison.Ordinal);
            }
            """;

        // Statement block — one brace-enclosed block, C# 11 double-dollar raw string.
        // With $$"""…""", a single { / } is a LITERAL brace in the emitted CSX (so this
        // block's own braces pass through verbatim) and {{hole}} is an interpolation hole.
        //
        // Engine-introduced locals carry the safeId suffix so two hello.console steps in
        // one suite never collide.  Engine types are referenced by fully-qualified name
        // (this provider does not reference Platform.Engine.Abstractions; the engine does
        // when it compiles the assembled script).  Cross-step state is read/written ONLY
        // through `Vars`.
        var block =
            $$"""
            {
                var __sw_{{safeId}} = System.Diagnostics.Stopwatch.StartNew();
                var __message_{{safeId}} = {{messageLiteral}};
                var __expected_{{safeId}} = {{expectedLiteral}};

                // Record the emitted greeting into Vars (state-via-Vars): the step's
                // visible side effect, observable by a later step or the report.
                Vars["hello::{{safeId}}"] = __message_{{safeId}};

                var __pass_{{safeId}} =
                    HelloConsole_Helpers.Check(__message_{{safeId}}, __expected_{{safeId}});
                __sw_{{safeId}}.Stop();

                var __verdict_{{safeId}} = __pass_{{safeId}}
                    ? Platform.Engine.Abstractions.Verdict.Pass
                    : Platform.Engine.Abstractions.Verdict.Fail;

                // Write the outcome under the engine's canonical key; the runner reads
                // it back after the isolated run.  VarKeys.Outcome is the single source
                // of truth for the key convention.  The structured observation literal is
                // built at emit time, so the body needs no JSON reference at runtime.
                Vars[Platform.Engine.Abstractions.VarKeys.Outcome("{{safeId}}")] =
                    new Platform.Engine.Abstractions.StepOutcome(
                        __verdict_{{safeId}},
                        __sw_{{safeId}}.ElapsedMilliseconds,
                        {{observationLiteral}});
            }
            """;

        return new CsxFragment(
            RequiredUsings: s_usings,
            RequiredHelpers: new[] { helper },
            StatementBlock: block);
    }
}
