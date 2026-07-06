// Community.Steps.JsonRpc.Tests — JsonRpcProviderTests.
//
// TWO test categories, mirroring the template's split:
//
//   1. CONFORMANCE — drives the provider through Validate -> Emit -> assemble ->
//      compile -> run-isolated, either via JsonRpcHarness (execution tests — see
//      JsonRpcHarness.cs for why a small custom harness is needed alongside the
//      published ProviderTestHarness) or via the plain, published
//      Platform.Sdk.Testing.ProviderTestHarness for the two tests that halt BEFORE
//      any Roslyn compile (schema / model validation rejection).
//
//   2. UNIT (emit surface) — calls JsonRpcProvider.Emit directly via TestCompileContext
//      and asserts the CsxFragment is well-formed per the CsxFragment composition
//      rules (§13.3.1).
//
// All conformance tests are Docker-free: JsonRpcTestServerFixture self-hosts a minimal
// JSON-RPC 2.0 responder on loopback (see JsonRpcTestServer.cs).
using Platform.Engine.Abstractions;
using Platform.Engine.Abstractions.Secrets;
using Platform.Sdk;
using Platform.Sdk.Testing;
using Platform.Sdk.Testing.Contexts;
using Xunit;

namespace Community.Steps.JsonRpc.Tests;

/// <summary>
/// Conformance and unit tests for the <c>rpc.json-rpc</c> sample provider.
/// </summary>
public sealed class JsonRpcProviderTests : IClassFixture<JsonRpcTestServerFixture>
{
    private readonly JsonRpcTestServer _server;

    public JsonRpcProviderTests(JsonRpcTestServerFixture fixture)
    {
        _server = fixture.Server;
    }

    // ── 1. Pass with expect.result satisfied ─────────────────────────────────

    [Fact]
    public async Task Conformance_SumMethod_ResultMatches_Pass()
    {
        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "sum",
            ParamsJson: "{\"a\":2,\"b\":3}",
            Notification: false,
            Expect: new JsonRpcExpect(Result: new[]
            {
                new JsonRpcResultAssertion("$.sum", "5"),
            }));

        var result = await JsonRpcHarness.RunAsync(model, "sum-pass");

        Assert.Equal(Verdict.Pass, result.Verdict);
    }

    // ── 2. Fail on expect.result mismatch ─────────────────────────────────────

    [Fact]
    public async Task Conformance_SumMethod_ResultMismatch_Fail()
    {
        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "sum",
            ParamsJson: "{\"a\":2,\"b\":3}",
            Notification: false,
            Expect: new JsonRpcExpect(Result: new[]
            {
                new JsonRpcResultAssertion("$.sum", "99"),
            }));

        var result = await JsonRpcHarness.RunAsync(model, "sum-mismatch");

        Assert.Equal(Verdict.Fail, result.Verdict);
        Assert.NotNull(result.Observation);
        Assert.Contains("resultMismatch", result.Observation, StringComparison.Ordinal);
    }

    // ── 3. Pass on expected error code (negative test) ───────────────────────

    [Fact]
    public async Task Conformance_UnknownMethod_ExpectedErrorCode_Pass()
    {
        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "does-not-exist",
            ParamsJson: null,
            Notification: false,
            Expect: new JsonRpcExpect(ErrorCode: -32601));

        var result = await JsonRpcHarness.RunAsync(model, "negative-test");

        Assert.Equal(Verdict.Pass, result.Verdict);
    }

    // ── 4. Fail when an unexpected JSON-RPC error arrives (bare call) ────────

    [Fact]
    public async Task Conformance_UnknownMethod_BareCall_Fail()
    {
        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "does-not-exist",
            ParamsJson: null,
            Notification: false,
            Expect: null);

        var result = await JsonRpcHarness.RunAsync(model, "unexpected-error");

        Assert.Equal(Verdict.Fail, result.Verdict);
        Assert.NotNull(result.Observation);
        Assert.Contains("unexpectedError", result.Observation, StringComparison.Ordinal);
    }

    // ── 5. EnvironmentError on connection-refused ────────────────────────────

    [Fact]
    public async Task Conformance_ConnectionRefused_EnvironmentError()
    {
        var closedUrl = JsonRpcTestServer.ReserveClosedPortUrl();
        var model = new JsonRpcModel(
            Url: closedUrl,
            Method: "sum",
            ParamsJson: "{\"a\":1,\"b\":1}",
            Notification: false,
            Expect: null);

        var result = await JsonRpcHarness.RunAsync(model, "connection-refused");

        Assert.Equal(Verdict.EnvironmentError, result.Verdict);
    }

    // ── 6. capture writes the variable ────────────────────────────────────────

    [Fact]
    public async Task Conformance_Capture_WritesVariable()
    {
        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "sum",
            ParamsJson: "{\"a\":4,\"b\":6}",
            Notification: false,
            Expect: null);

        var captures = new Dictionary<string, CaptureExpr>(StringComparer.Ordinal)
        {
            ["capturedSum"] = new CaptureExpr(CaptureFormat.JsonPath, "$.result.sum"),
        };

        var result = await JsonRpcHarness.RunAsync(model, "capture-write", captures: captures);

        Assert.Equal(Verdict.Pass, result.Verdict);
        Assert.True(result.Vars.TryGetValue("capturedSum", out var captured),
            $"Vars must contain the captured variable. Keys present: [{string.Join(", ", result.Vars.Keys)}].");
        Assert.Equal("10", captured);
    }

    // ── Substitution: {placeholder} / ${secret:...} resolution ───────────────
    //
    // MINOR-1 follow-up: the original sample only ever ran `url` through
    // Secret_Helpers.ResolveTemplate; `method` and every string leaf of `params` were
    // emitted raw. These tests exercise the fix from the OUTSIDE — asserting on what
    // actually reached the server or on the server's own resolved response — rather
    // than inspecting the emitted CSX text, so they would have caught the original bug.

    // (a) `url` entirely a {placeholder}, pre-seeded into Vars (mirrors how the real
    // engine pipeline pre-loads the scenario's `variables:` section before any step
    // runs). If the placeholder were NOT resolved, `new Uri("{rpcBaseUrl}", Absolute)`
    // throws UriFormatException -> caught by the generic catch -> EnvironmentError, not
    // Pass — so a Pass here is only possible when substitution genuinely happened.
    [Fact]
    public async Task Conformance_UrlPlaceholder_PreSeededVariable_ResolvesAndCalls()
    {
        var model = new JsonRpcModel(
            Url: "{rpcBaseUrl}",
            Method: "sum",
            ParamsJson: "{\"a\":2,\"b\":3}",
            Notification: false,
            Expect: new JsonRpcExpect(Result: new[]
            {
                new JsonRpcResultAssertion("$.sum", "5"),
            }));

        var preSeed = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rpcBaseUrl"] = _server.BaseUrl,
        };

        var result = await JsonRpcHarness.RunAsync(model, "url-placeholder", preSeedVars: preSeed);

        Assert.Equal(Verdict.Pass, result.Verdict);
    }

    // (b1) `params` as a named-params OBJECT (README example 1/3 shape) carrying a
    // {placeholder} leaf. Calls the test server's "echo" method, which returns
    // result = the params it received verbatim — so asserting on $.orderId proves what
    // the SERVER actually saw, not what the provider intended to send. Before the fix
    // this would Fail with a resultMismatch against the literal string "{orderId}".
    [Fact]
    public async Task Conformance_ParamsObjectLeaf_Placeholder_ResolvesBeforeSend()
    {
        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "echo",
            ParamsJson: "{\"orderId\":\"{orderId}\"}",
            Notification: false,
            Expect: new JsonRpcExpect(Result: new[]
            {
                new JsonRpcResultAssertion("$.orderId", "\"ORD-42\""),
            }));

        var preSeed = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["orderId"] = "ORD-42",
        };

        var result = await JsonRpcHarness.RunAsync(model, "params-object-leaf", preSeedVars: preSeed);

        Assert.Equal(Verdict.Pass, result.Verdict);
    }

    // (b2) `params` as a positional-params ARRAY (README example 4 shape) carrying a
    // {placeholder} leaf alongside a literal element — proves the walker recurses
    // correctly into an array and leaves non-placeholder leaves untouched.
    [Fact]
    public async Task Conformance_ParamsArrayLeaf_Placeholder_ResolvesBeforeSend()
    {
        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "echo",
            ParamsJson: "[\"order.shipped\",\"{orderId}\"]",
            Notification: false,
            Expect: new JsonRpcExpect(Result: new[]
            {
                new JsonRpcResultAssertion("$[0]", "\"order.shipped\""),
                new JsonRpcResultAssertion("$[1]", "\"ORD-99\""),
            }));

        var preSeed = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["orderId"] = "ORD-99",
        };

        var result = await JsonRpcHarness.RunAsync(model, "params-array-leaf", preSeedVars: preSeed);

        Assert.Equal(Verdict.Pass, result.Verdict);
    }

    // (c) `${secret:env/...}` in `url`, resolved through a REAL SecretAccessor wired to
    // the published EnvironmentSecretResolver (Platform.Engine.Abstractions.Secrets) —
    // usable here because both types are published (not engine-internal), unlike the
    // Vault resolver. Proves the secret-token half of ResolveTemplate end to end, not
    // just the placeholder half.
    [Fact]
    public async Task Conformance_UrlSecretToken_EnvSource_ResolvesAndCalls()
    {
        const string envVarName = "VOUCHFX_JSONRPC_SAMPLE_TEST_URL";
        Environment.SetEnvironmentVariable(envVarName, _server.BaseUrl);
        try
        {
            var model = new JsonRpcModel(
                Url: $"${{secret:env/{envVarName}}}",
                Method: "sum",
                ParamsJson: "{\"a\":10,\"b\":5}",
                Notification: false,
                Expect: new JsonRpcExpect(Result: new[]
                {
                    new JsonRpcResultAssertion("$.sum", "15"),
                }));

            var secrets = new SecretAccessor(
                new SecretSourceCatalog(new ISecretResolver[] { new EnvironmentSecretResolver() }));

            var result = await JsonRpcHarness.RunAsync(model, "url-secret-env", secrets: secrets);

            Assert.Equal(Verdict.Pass, result.Verdict);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    // ── 7. verifyMode: RETRY converging on the flaky method -> Pass ──────────

    [Fact]
    public async Task Conformance_RetryOnFlakyMethod_Converges_Pass()
    {
        _server.ResetFlaky();
        _server.FlakyConvergeAfterCalls = 3;

        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "flaky",
            ParamsJson: null,
            Notification: false,
            Expect: new JsonRpcExpect(Result: new[]
            {
                new JsonRpcResultAssertion("$", "\"converged\""),
            }));

        var result = await JsonRpcHarness.RunAsync(
            model, "flaky-converge", retry: true, timeoutMs: 5000);

        Assert.Equal(Verdict.Pass, result.Verdict);
    }

    // ── 8. RETRY exhausted -> Inconclusive-shaped outcome ─────────────────────

    [Fact]
    public async Task Conformance_RetryNeverConverges_Inconclusive()
    {
        _server.ResetFlaky();
        _server.FlakyConvergeAfterCalls = 1_000; // never reached within the short window below

        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "flaky",
            ParamsJson: null,
            Notification: false,
            Expect: new JsonRpcExpect(Result: new[]
            {
                new JsonRpcResultAssertion("$", "\"converged\""),
            }));

        var result = await JsonRpcHarness.RunAsync(
            model, "flaky-exhausted", retry: true, timeoutMs: 1500);

        // The engine's RetryRunner (Platform.Engine.Abstractions.Retry.RetryRunner)
        // converts a sustained Fail into Inconclusive once the polling window elapses
        // (RetryRunner.TimedOut / .Classify in the engine repo) — asserted here as the
        // actual, observed result, not assumed.
        Assert.Equal(Verdict.Inconclusive, result.Verdict);
    }

    // ── Bonus: notification transport-success path ───────────────────────────

    [Fact]
    public async Task Conformance_Notification_TransportSuccess_Pass()
    {
        var model = new JsonRpcModel(
            Url: _server.BaseUrl,
            Method: "sum",
            ParamsJson: "{\"a\":1,\"b\":1}",
            Notification: true,
            Expect: null);

        var result = await JsonRpcHarness.RunAsync(model, "notify-ok");

        Assert.Equal(Verdict.Pass, result.Verdict);
    }

    // ── Schema/validation halt path (halts BEFORE compile — safe on the published harness) ──

    [Fact]
    public async Task Conformance_MissingUrlAndMethod_FailsSchemaValidation()
    {
        const string yaml = """
            steps:
              - id: broken
                type: rpc.json-rpc
                params:
                  a: 1
            """;

        var result = await ProviderTestHarness.RunSingleStepAsync(
            yaml,
            typeof(JsonRpcProvider).Assembly,
            "broken");

        Assert.NotEmpty(result.SchemaErrors);
        Assert.Null(result.Verdict);
    }

    [Fact]
    public async Task Conformance_ExpectResultAndErrorBothDeclared_FailsModelValidation()
    {
        const string yaml = """
            steps:
              - id: both-expects
                type: rpc.json-rpc
                url: "http://localhost:1/rpc"
                method: sum
                expect:
                  result:
                    - path: "$.sum"
                      value: 5
                  error:
                    code: -32601
            """;

        var result = await ProviderTestHarness.RunSingleStepAsync(
            yaml,
            typeof(JsonRpcProvider).Assembly,
            "both-expects");

        Assert.NotEmpty(result.ValidationErrors);
        Assert.Null(result.Verdict);
    }

    // An explicit but EMPTY 'expect.result: []' must be rejected rather than silently
    // degrading to bare-call semantics (peer-review follow-up finding #1).
    [Fact]
    public async Task Conformance_EmptyExpectResultList_FailsModelValidation()
    {
        const string yaml = """
            steps:
              - id: empty-result
                type: rpc.json-rpc
                url: "http://localhost:1/rpc"
                method: sum
                expect:
                  result: []
            """;

        var result = await ProviderTestHarness.RunSingleStepAsync(
            yaml,
            typeof(JsonRpcProvider).Assembly,
            "empty-result");

        Assert.NotEmpty(result.ValidationErrors);
        Assert.Null(result.Verdict);
    }

    // MINOR-2: JSON-RPC 2.0 §4.2 requires params to be structured (an object or an
    // array) when present — rejected at the SCHEMA layer, before Bind/Validate ever run.
    [Fact]
    public async Task Conformance_ScalarParams_FailsSchemaValidation()
    {
        const string yaml = """
            steps:
              - id: scalar-params
                type: rpc.json-rpc
                url: "http://localhost:1/rpc"
                method: sum
                params: 42
            """;

        var result = await ProviderTestHarness.RunSingleStepAsync(
            yaml,
            typeof(JsonRpcProvider).Assembly,
            "scalar-params");

        Assert.NotEmpty(result.SchemaErrors);
        Assert.Null(result.Verdict);
    }

    // MINOR-2 defence-in-depth: the same constraint enforced directly at
    // JsonRpcProvider.Validate, exercised via a hand-built model (bypassing the schema
    // layer entirely) so the model-level check is proven independently of the schema one.
    [Fact]
    public void Unit_Validate_ScalarParams_Rejected()
    {
        var provider = new JsonRpcProvider();
        var model = new JsonRpcModel(
            Url: "http://localhost/rpc",
            Method: "sum",
            ParamsJson: "42",
            Notification: false,
            Expect: null);

        var result = provider.Validate(model, new TestProjectContext());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("params", StringComparison.OrdinalIgnoreCase));
    }

    // ── Emit-shape unit tests (mirrors the template/Core pattern) ────────────

    [Fact]
    public void Unit_Emit_FragmentSatisfiesCompositionRules()
    {
        var provider = new JsonRpcProvider();
        var model = new JsonRpcModel("http://localhost/rpc", "ping", null, false, null);
        var ctx = new TestCompileContext(stepId: "ping-step");

        var fragment = provider.Emit(model, ctx);

        // RequiredUsings: bare namespace strings only (§13.3.1).
        Assert.Contains("System.Net.Http", fragment.RequiredUsings);
        Assert.Contains("Platform.Engine.Abstractions", fragment.RequiredUsings);
        Assert.All(fragment.RequiredUsings, u => Assert.False(
            u.TrimStart().StartsWith("using ", StringComparison.Ordinal),
            $"RequiredUsings must be bare namespace strings, got: '{u}'"));

        // RequiredHelpers: provider-id-prefixed helper class present (§13.3.1).
        Assert.NotEmpty(fragment.RequiredHelpers);
        Assert.Contains(
            fragment.RequiredHelpers,
            h => h.Contains("RpcJsonRpc_Helpers", StringComparison.Ordinal));

        // StatementBlock: brace-enclosed block (§13.3.1).
        var block = fragment.StatementBlock;
        Assert.StartsWith("{", block.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("}", block.TrimEnd(), StringComparison.Ordinal);

        // No 'using var' anywhere in the emitted body or helpers (illegal in a Roslyn
        // script body, §13.3.1).
        Assert.DoesNotContain("using var", block, StringComparison.Ordinal);
        Assert.All(fragment.RequiredHelpers, h =>
            Assert.DoesNotContain("using var", h, StringComparison.Ordinal));
    }

    [Fact]
    public void Unit_Emit_HyphenatedStepId_SanitisedForOutcomeKey()
    {
        var provider = new JsonRpcProvider();
        var model = new JsonRpcModel("http://localhost/rpc", "ping", null, false, null);
        var ctx = new TestCompileContext(stepId: "my-rpc-step");

        var fragment = provider.Emit(model, ctx);

        // The sanitised id (hyphens -> underscores, CsxFragment.SanitiseId) is used for
        // the C#-identifier-safe `stepId` argument threaded into
        // VarKeys.Outcome/CaptureStatus at runtime.
        Assert.Contains("\"my_rpc_step\"", fragment.StatementBlock, StringComparison.Ordinal);

        // The RAW (un-sanitised) step id is ALSO present in the block — but only as the
        // JSON-RPC request `id` DATA VALUE (a deliberate design choice: "id = the step
        // id unless notification"), never spliced into a C# identifier. Both literals
        // are distinct string arguments to the same helper call.
        Assert.Contains("\"my-rpc-step\"", fragment.StatementBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Unit_Emit_NotificationWithCapture_ShortCircuitsToFail()
    {
        var provider = new JsonRpcProvider();
        var model = new JsonRpcModel("http://localhost/rpc", "ping", null, true, null);
        var captures = new Dictionary<string, CaptureExpr>(StringComparer.Ordinal)
        {
            ["x"] = new CaptureExpr(CaptureFormat.JsonPath, "$.x"),
        };
        var ctx = new TestCompileContext(stepId: "notify-with-capture", captureExprs: captures);

        var fragment = provider.Emit(model, ctx);

        // notification + capture cannot be rejected in Validate (IProjectContext has no
        // capture view) — Emit instead short-circuits to a trivial Fail block with no
        // HTTP call and no helper class required. Fail, not Inconclusive: this is an
        // author misconfiguration (MINOR-5), not a timing uncertainty, and Inconclusive
        // does not break CI by default (§12.1).
        Assert.Contains("Verdict.Fail", fragment.StatementBlock, StringComparison.Ordinal);
        Assert.Empty(fragment.RequiredHelpers);
    }
}
