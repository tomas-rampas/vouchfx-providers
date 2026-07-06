// Community.Steps.JsonRpc — rpc.json-rpc step provider (JSON-RPC 2.0 over HTTP).
//
// A richer worked SAMPLE (not a Verified submission) for the vouchfx community hub:
// a real BCL-plus-JsonPath.Net transport provider, mirroring the engine's own Core
// http.rest / mail-expect.smtp providers, but living entirely outside the engine repo
// against the frozen v1 Platform.Sdk contract.  See samples/Community.Steps.JsonRpc/README.md
// for worked .e2e.yaml examples, the full verdict-mapping table, and known limitations.
//
// §5.6 ASSEMBLY-GRAPH HYGIENE: namespace is Community.Steps.JsonRpc — never
// Platform.Steps.* / Platform.Engine.* (reserved, refused at suite startup).
//
// §5 MEMORY MODEL: this provider takes NO reference to Platform.Engine.Abstractions.
// The emitted CSX reaches the run environment ONLY through the engine-injected
// Vars/Secrets globals and refers to engine types (Verdict, StepOutcome, VarKeys,
// ISecretAccessor, SecretResolutionException) by fully-qualified name inside the
// emitted text — the engine already references Platform.Engine.Abstractions when it
// compiles the assembled script, so no static handle from this provider bridges the
// collectible AssemblyLoadContext boundary.
//
// RETRY model: verifyMode: RETRY is a purely engine-side wrapper (CsxAssembler /
// Platform.Engine.Abstractions.Retry.RetryRunner) that re-invokes ANY provider's
// emitted block unchanged — there is no provider-side "RETRY capability" interface
// or flag to declare.  The ONLY contract this provider must honour is: write exactly
// one Platform.Engine.Abstractions.StepOutcome into Vars[VarKeys.Outcome(stepId)] on
// every invocation, and never write Verdict.Inconclusive for a merely-not-yet-matching
// assertion — RpcJsonRpc_Helpers writes Fail for a mismatch, exactly like http.rest's
// status assertion; the engine's RetryRunner is what turns a sustained Fail into
// Inconclusive once the step's timeout elapses (verified against
// Platform.Engine.Abstractions.Retry.RetryRunner in the engine repo).
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Platform.Sdk;
using YamlDotNet.RepresentationModel;

namespace Community.Steps.JsonRpc;

/// <summary>
/// Sample provider for the <c>rpc.json-rpc</c> step kind — JSON-RPC 2.0 requests over
/// HTTP (the wire protocol used by LSP, Ethereum/Bitcoin Core JSON-RPC APIs, and many
/// internal RPC services).
/// </summary>
/// <remarks>
/// <para>
/// Implements the four mandatory <see cref="Platform.Sdk"/> provider interfaces
/// (<see cref="IStepProvider"/>, <see cref="IStepBinder{TModel}"/>,
/// <see cref="IStepValidator{TModel}"/>, <see cref="IStepCompiler{TModel}"/>) plus two
/// optional ones that are both part of the frozen v1 SDK surface:
/// <see cref="ICompileReferenceContributor"/> (this provider's emitted CSX genuinely
/// needs <c>System.Net.Http</c>, <c>System.Text.Json</c>, <c>System.Private.Uri</c> and
/// <c>JsonPath.Net</c> at Roslyn compile time — none of which is in the engine's
/// minimal default script reference set, verified against
/// <c>RoslynScriptCompiler.BuildTpaReferences</c> in the engine repo) and
/// <see cref="IStepDiffRenderer"/> (mirroring how the Core <c>mail-expect.smtp</c>
/// provider renders an expected-vs-observed diff for its own Fail-observation shapes).
/// It does <em>not</em> implement <see cref="IResourceContributor{TModel}"/> — this
/// provider needs no Aspire-managed infrastructure; the target is whatever absolute
/// URL the author supplies.
/// </para>
/// <para>
/// <strong>Verdict-mapping decision tree</strong> (see the README for the full table
/// with worked examples):
/// <list type="bullet">
///   <item>Connection refused / DNS failure / TLS failure / a response body that is
///   not valid JSON → <c>Verdict.EnvironmentError</c> — a run-environment problem, not
///   a product defect.</item>
///   <item>A client-side timeout (<c>TaskCanceledException</c> / <c>TimeoutException</c>)
///   → <c>Verdict.Inconclusive</c> — mirrors the Core <c>http.rest</c> provider's own
///   convention: "the test could not complete due to a stall."</item>
///   <item><c>expect.result</c> declared: a JSON-RPC <c>error</c> envelope, a response
///   <c>id</c> mismatch, or any JSONPath assertion mismatch → <c>Verdict.Fail</c>;
///   all assertions matching → <c>Verdict.Pass</c>.</item>
///   <item><c>expect.error.code</c> declared (a negative test): a <c>result</c>
///   envelope instead of an error, a missing error, or a code mismatch →
///   <c>Verdict.Fail</c>; a matching code → <c>Verdict.Pass</c>.</item>
///   <item>Neither declared (a "bare" call): an unexpected <c>error</c> envelope or an
///   <c>id</c> mismatch → <c>Verdict.Fail</c>; otherwise → <c>Verdict.Pass</c>.</item>
///   <item><c>notification: true</c>: no envelope is parsed at all — only transport-level
///   success (2xx) is asserted.</item>
///   <item>The engine-standard <c>capture:</c> field is evaluated with JSONPath.Net
///   against the FULL response envelope (not just <c>result</c>) — an unmet capture
///   downgrades an otherwise-Pass verdict to <c>Verdict.Inconclusive</c> with reason
///   <c>captureUnmet</c>, mirroring <c>http.rest</c>'s "upstream-capture-unmet"
///   convention exactly.</item>
/// </list>
/// </para>
/// </remarks>
[StepProvider]
public sealed class JsonRpcProvider
    : IStepProvider,
      IStepBinder<JsonRpcModel>,
      IStepValidator<JsonRpcModel>,
      IStepCompiler<JsonRpcModel>,
      ICompileReferenceContributor,
      IStepDiffRenderer
{
    // ── ICompileReferenceContributor ──────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Returns the defining assemblies for every type the emitted
    /// <c>RpcJsonRpc_Helpers</c> class spells by name at runtime:
    /// <c>System.Net.Http.HttpClient</c>/<c>HttpClientHandler</c> (System.Net.Http),
    /// <c>System.Text.Json.Nodes.JsonNode</c>/<c>JsonSerializer</c> (System.Text.Json),
    /// <c>System.Globalization.CultureInfo</c> (used for invariant-culture integer
    /// formatting in the observation JSON), <c>System.Uri</c> (System.Private.Uri —
    /// type-forwarded from System.Runtime, but Roslyn requires the defining assembly
    /// as an explicit reference to avoid CS1069, exactly as the Core
    /// <c>mail-expect.smtp</c> provider documents), and <c>Json.Path.JsonPath</c>
    /// (JsonPath.Net) for both the <c>expect.result</c> assertions and the
    /// engine-standard <c>capture:</c> field.  All are already loaded in the Default
    /// ALC and are never loaded into the collectible ALC (§5 memory-model invariant).
    /// </remarks>
    public IEnumerable<System.Reflection.Assembly> CompileReferenceAssemblies
    {
        get
        {
            yield return typeof(System.Net.Http.HttpClient).Assembly;
            yield return typeof(JsonSerializer).Assembly;
            yield return typeof(CultureInfo).Assembly;
            yield return typeof(Uri).Assembly;
            yield return typeof(Json.Path.JsonPath).Assembly;
        }
    }

    // ── IStepProvider ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public StepKindId Kind { get; } = new StepKindId("rpc", "json-rpc");

    /// <inheritdoc />
    public ProviderMetadata Metadata { get; } = new ProviderMetadata(
        Version: "1.0.0",
        MinEngineVersion: "1.0.0",
        License: "Apache-2.0",
        Authors: new[] { "vouchfx-community" });

    // ── IStepBinder<JsonRpcModel> ──────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Does NOT declare the <c>type</c> discriminator — the engine's
    /// <c>SchemaComposer</c> derives it from <see cref="Kind"/> (§13.6).  Note there is
    /// deliberately no <c>headers</c> field — see the README "Known limitations".
    /// </remarks>
    public JsonSchemaFragment SchemaFragment { get; } = new JsonSchemaFragment(
        """
        {
          "type": "object",
          "required": ["url", "method"],
          "properties": {
            "url": {
              "description": "Absolute target URL. May contain {placeholder} tokens and ${secret:source/path} references, both resolved at step-execution time.",
              "type": "string"
            },
            "method": {
              "description": "The JSON-RPC method name.",
              "type": "string"
            },
            "params": {
              "description": "JSON-RPC params. A YAML mapping becomes a named params object; a YAML sequence becomes a positional params array. Per JSON-RPC 2.0 §4.2, params MUST be structured (an object or an array) when present — a bare scalar is rejected.",
              "type": ["object", "array"]
            },
            "notification": {
              "description": "When true, sends a JSON-RPC notification (no id, fire-and-forget): only transport-level success (a 2xx status) is asserted, and the response body is never parsed. Incompatible with 'expect' and with 'capture'. Defaults to false.",
              "type": "boolean",
              "default": false
            },
            "expect": {
              "description": "Optional assertion block. 'result' and 'error' are mutually exclusive.",
              "type": "object",
              "properties": {
                "result": {
                  "description": "JSONPath assertions evaluated against the response envelope's $.result.",
                  "type": "array",
                  "items": {
                    "type": "object",
                    "required": ["path", "value"],
                    "properties": {
                      "path": { "type": "string" },
                      "value": {}
                    },
                    "additionalProperties": false
                  }
                },
                "error": {
                  "description": "Negative-test assertion: the response MUST be a JSON-RPC error with this code.",
                  "type": "object",
                  "required": ["code"],
                  "properties": {
                    "code": { "type": "integer" }
                  },
                  "additionalProperties": false
                }
              },
              "additionalProperties": false
            }
          },
          "additionalProperties": true
        }
        """);

    /// <inheritdoc />
    /// <remarks>
    /// BIND → VALIDATE CONTRACT (§13): Bind only shapes YAML into the model; it never
    /// rejects input (that is <see cref="Validate"/>'s job).  A non-mapping node yields
    /// an empty model that <see cref="Validate"/> then rejects with a clear diagnostic.
    /// </remarks>
    public JsonRpcModel Bind(YamlNode node, IBindingContext ctx)
    {
        if (node is not YamlMappingNode mapping)
        {
            return new JsonRpcModel(
                Url: string.Empty,
                Method: string.Empty,
                ParamsJson: null,
                Notification: false,
                Expect: null);
        }

        var url = GetScalar(mapping, "url");
        var method = GetScalar(mapping, "method");
        var paramsJson = BindParams(mapping);
        var notification = GetBool(mapping, "notification");
        var expect = BindExpect(mapping);

        return new JsonRpcModel(
            Url: url,
            Method: method,
            ParamsJson: paramsJson,
            Notification: notification,
            Expect: expect);
    }

    private static string? BindParams(YamlMappingNode mapping)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode("params"), out var paramsNode))
            return null;

        var jsonNode = YamlNodeToJson(paramsNode);
        return jsonNode is null ? null : jsonNode.ToJsonString();
    }

    private static JsonRpcExpect? BindExpect(YamlMappingNode mapping)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode("expect"), out var expectNode)
            || expectNode is not YamlMappingNode expectMap)
        {
            return null;
        }

        IReadOnlyList<JsonRpcResultAssertion>? result = null;
        if (expectMap.Children.TryGetValue(new YamlScalarNode("result"), out var resultNode)
            && resultNode is YamlSequenceNode resultSeq)
        {
            var list = new List<JsonRpcResultAssertion>();
            foreach (var item in resultSeq.Children)
            {
                if (item is not YamlMappingNode itemMap)
                    continue;

                var path = GetScalar(itemMap, "path");
                var valueJson = "null";
                if (itemMap.Children.TryGetValue(new YamlScalarNode("value"), out var valueNode))
                {
                    var converted = YamlNodeToJson(valueNode);
                    valueJson = converted is null ? "null" : converted.ToJsonString();
                }

                list.Add(new JsonRpcResultAssertion(Path: path, ExpectedJson: valueJson));
            }

            result = list;
        }

        int? errorCode = null;
        if (expectMap.Children.TryGetValue(new YamlScalarNode("error"), out var errorNode)
            && errorNode is YamlMappingNode errorMap
            && errorMap.Children.TryGetValue(new YamlScalarNode("code"), out var codeNode)
            && codeNode is YamlScalarNode codeScalar
            && int.TryParse(codeScalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
        {
            errorCode = code;
        }

        return result is null && errorCode is null ? null : new JsonRpcExpect(result, errorCode);
    }

    /// <summary>
    /// Converts a YAML node (mapping / sequence / scalar) into a
    /// <see cref="JsonNode"/> so a structured YAML <c>params</c> or assertion
    /// <c>value</c> can be re-serialised as JSON.  A quoted scalar is always a string
    /// (the author's explicit intent); an unquoted scalar follows YAML 1.1 typing
    /// (bool / null / integer / float tokens), and everything else is a string.
    /// Mirrors the Core <c>http.rest</c> provider's own YAML→JSON conversion.
    /// </summary>
    private static JsonNode? YamlNodeToJson(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode map:
                var obj = new JsonObject();
                foreach (var (k, v) in map.Children)
                {
                    var key = k is YamlScalarNode ks ? ks.Value ?? string.Empty : k.ToString();
                    obj[key] = YamlNodeToJson(v);
                }
                return obj;

            case YamlSequenceNode seq:
                var arr = new JsonArray();
                foreach (var item in seq.Children)
                    arr.Add(YamlNodeToJson(item));
                return arr;

            case YamlScalarNode scalar:
                return ScalarToJson(scalar);

            default:
                return JsonValue.Create(node.ToString());
        }
    }

    private static JsonValue? ScalarToJson(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;

        if (scalar.Style is YamlDotNet.Core.ScalarStyle.SingleQuoted or YamlDotNet.Core.ScalarStyle.DoubleQuoted)
            return JsonValue.Create(value);

        if (value.Length == 0)
            return JsonValue.Create(string.Empty);

        if (value is "null" or "Null" or "NULL" or "~")
            return null;

        if (value is "true" or "True" or "TRUE")
            return JsonValue.Create(true);
        if (value is "false" or "False" or "FALSE")
            return JsonValue.Create(false);

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            return JsonValue.Create(l);
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return JsonValue.Create(d);

        return JsonValue.Create(value);
    }

    private static string GetScalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node)
        && node is YamlScalarNode scalar
            ? scalar.Value ?? string.Empty
            : string.Empty;

    private static bool GetBool(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node)
        && node is YamlScalarNode scalar
        && bool.TryParse(scalar.Value, out var parsed)
        && parsed;

    // ── IStepValidator<JsonRpcModel> ───────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Validates <c>url</c>/<c>method</c> presence, the <c>expect.result</c> /
    /// <c>expect.error</c> mutual exclusion, and the <c>notification</c> /
    /// <c>expect</c> incompatibility.  It deliberately does NOT reject
    /// <c>notification</c> + <c>capture</c> here — <see cref="IProjectContext"/>
    /// carries no view of the step's <c>capture</c> map (only
    /// <see cref="ICompileContext"/> does, at the later Emit stage) — see
    /// <see cref="Emit"/> for how that combination is instead handled at compile time.
    /// </remarks>
    public ValidationResult Validate(JsonRpcModel model, IProjectContext ctx)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(model.Url))
        {
            errors.Add("rpc.json-rpc: 'url' must not be empty.");
        }
        else if (!model.Url.Contains('{', StringComparison.Ordinal)
                 && !model.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                 && !model.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                "rpc.json-rpc: 'url' must be an absolute http(s) URL (or a {placeholder}-templated " +
                "value that resolves to one at execution time).");
        }

        if (string.IsNullOrWhiteSpace(model.Method))
            errors.Add("rpc.json-rpc: 'method' must not be empty.");

        // JSON-RPC 2.0 §4.2: params MUST be structured (an object or an array) when
        // present. The schema fragment already constrains this at the YAML/JSON-Schema
        // layer (SchemaFragment, above); this is defence-in-depth at the bound-model
        // layer, mirroring how other structural constraints here are enforced twice.
        if (model.ParamsJson is not null)
        {
            var paramsNode = JsonNode.Parse(model.ParamsJson);
            if (paramsNode is not JsonObject && paramsNode is not JsonArray)
            {
                errors.Add(
                    "rpc.json-rpc: 'params' must be a mapping (named params) or a " +
                    "sequence (positional params) per JSON-RPC 2.0 §4.2 — a scalar " +
                    "value is not permitted.");
            }
        }

        var hasResult = model.Expect?.Result is { Count: > 0 };
        var hasError = model.Expect?.ErrorCode is not null;

        // An explicit but EMPTY 'expect.result: []' silently degraded to bare-call
        // semantics before this check existed (hasResult above requires Count > 0), which
        // is weaker than what an author writing an empty list almost certainly intended
        // ("assert a result exists"). Reject it with a clear diagnostic instead of
        // quietly downgrading it.
        if (model.Expect?.Result is { Count: 0 })
        {
            errors.Add(
                "rpc.json-rpc: 'expect.result' must contain at least one assertion; " +
                "omit 'expect' entirely for a bare call.");
        }

        if (hasResult && hasError)
        {
            errors.Add(
                "rpc.json-rpc: 'expect.result' and 'expect.error' are mutually exclusive; " +
                "declare at most one.");
        }

        if (model.Notification && (hasResult || hasError))
        {
            errors.Add(
                "rpc.json-rpc: 'notification: true' is incompatible with 'expect' — a " +
                "fire-and-forget notification has no response body to assert on.");
        }

        return errors.Count == 0
            ? ValidationResult.Success
            : ValidationResult.Failure(errors.ToArray());
    }

    // ── CsxFragment components ────────────────────────────────────────────────

    private static readonly IReadOnlyList<string> s_usings = new[]
    {
        "System",
        "System.Collections.Generic",
        "System.Net.Http",
        "System.Diagnostics",
        "System.Threading.Tasks",
        "Platform.Engine.Abstractions",
    };

    /// <summary>
    /// Namespaces needed by the trivial notification+capture short-circuit block (see
    /// <see cref="Emit"/>) — a small subset of <see cref="s_usings"/>.
    /// </summary>
    private static readonly IReadOnlyList<string> s_shortCircuitUsings = new[]
    {
        "System",
        "Platform.Engine.Abstractions",
    };

    /// <summary>
    /// Full source of the provider-id-prefixed helper class (§13.3.1).  The class
    /// name begins with <c>RpcJsonRpc_</c> (family <c>rpc</c> + provider
    /// <c>json-rpc</c>, PascalCase-concatenated — mirrors <c>MailExpectSmtp_</c> for
    /// family <c>mail-expect</c> + provider <c>smtp</c>).  Byte-identical across every
    /// <c>rpc.json-rpc</c> step in a suite (§13.3.1 dedup rule) — it carries no
    /// per-step interpolation; all per-step data arrives as arguments.
    /// </summary>
    private const string HelperSource =
        """
        static class RpcJsonRpc_Helpers
        {
            // Builds the JSON-RPC 2.0 request envelope, POSTs it, classifies the
            // response into a Verdict (see the provider's XML docs / README for the
            // full decision tree), evaluates the engine-standard `capture:` field via
            // JsonPath.Net against the FULL envelope, and writes a StepOutcome into
            // Vars[VarKeys.Outcome(stepId)].
            //
            // IDEMPOTENT: every invocation performs one fresh HTTP call and writes a
            // fresh outcome — this is what lets verifyMode: RETRY simply re-invoke this
            // method unchanged (the engine's RetryRunner owns the poll loop and the
            // eventual Fail/Inconclusive -> Inconclusive-on-timeout conversion; this
            // helper itself NEVER writes Inconclusive for a not-yet-matching assertion,
            // only for a timeout or an unmet capture).
            public static async System.Threading.Tasks.Task ExecuteAsync(
                System.Collections.Generic.IDictionary<string, object?> vars,
                Platform.Engine.Abstractions.Secrets.ISecretAccessor secrets,
                string stepId,
                string urlTemplate,
                string method,
                string? paramsJson,
                bool notification,
                string requestId,
                string[] resultPaths,
                string[] resultExpectedJson,
                int? expectedErrorCode,
                string[] captureVarNames,
                string[] captureExprs)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var verdict = Platform.Engine.Abstractions.Verdict.EnvironmentError;
                var observation = "{\"error\":\"unexpected\"}";

                var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };
                var client = new System.Net.Http.HttpClient(handler, disposeHandler: true);
                try
                {
                    client.Timeout = System.TimeSpan.FromSeconds(30);
                    client.MaxResponseContentBufferSize = 16 * 1024 * 1024;

                    // Resolve {placeholder} + ${secret:source/path} tokens INSIDE the guarded
                    // region (§17), in a single pass, exactly as http.rest resolves its 'path'.
                    var url = Secret_Helpers.ResolveTemplate(secrets, vars, urlTemplate);
                    var uri = new System.Uri(url, System.UriKind.Absolute);
                    if (!string.Equals(uri.Scheme, System.Uri.UriSchemeHttp, System.StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(uri.Scheme, System.Uri.UriSchemeHttps, System.StringComparison.OrdinalIgnoreCase))
                    {
                        throw new System.InvalidOperationException(
                            "rpc.json-rpc: resolved url scheme '" + uri.Scheme + "' is not http/https.");
                    }

                    // Resolve `method` the same way as `url` — a single ResolveTemplate
                    // pass inside the guarded region (§17) — so a {placeholder} or
                    // ${secret:...} token in the method name is honoured, not sent literally.
                    var resolvedMethod = Secret_Helpers.ResolveTemplate(secrets, vars, method);

                    var envelope = new System.Text.Json.Nodes.JsonObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["method"] = resolvedMethod,
                    };
                    if (paramsJson is not null)
                    {
                        // `params` is parsed into a JsonNode tree FIRST, then every STRING
                        // LEAF value is resolved in place via ResolveParamsLeaves — never by
                        // template-substituting the raw JSON text before parsing. Per-leaf
                        // assignment through JsonNode is injection-safe: a resolved value can
                        // only ever replace one JSON string value with another, so a
                        // substituted value containing a quote or brace cannot corrupt the
                        // envelope the way raw-text substitution could.
                        var paramsNode = System.Text.Json.Nodes.JsonNode.Parse(paramsJson);
                        ResolveParamsLeaves(paramsNode, secrets, vars);
                        envelope["params"] = paramsNode;
                    }
                    if (!notification)
                        envelope["id"] = requestId;

                    using (var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, uri))
                    {
                        req.Content = new System.Net.Http.StringContent(
                            envelope.ToJsonString(), System.Text.Encoding.UTF8, "application/json");

                        var resp = await client.SendAsync(req).ConfigureAwait(false);
                        var bodyStr = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var statusCode = (int)resp.StatusCode;

                        if (notification)
                        {
                            // Fire-and-forget: assert transport success only. No envelope is read.
                            var ok = statusCode is >= 200 and < 300;
                            verdict = ok ? Platform.Engine.Abstractions.Verdict.Pass : Platform.Engine.Abstractions.Verdict.Fail;
                            observation = "{\"notification\":true,\"status\":" + statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
                        }
                        else
                        {
                            System.Text.Json.Nodes.JsonNode? envNode = null;
                            try { envNode = System.Text.Json.Nodes.JsonNode.Parse(bodyStr); }
                            catch (System.Exception) { envNode = null; }

                            if (envNode is not System.Text.Json.Nodes.JsonObject envObj)
                            {
                                verdict = Platform.Engine.Abstractions.Verdict.EnvironmentError;
                                observation = "{\"error\":\"non-json-envelope\"}";
                            }
                            else
                            {
                                var hasError = envObj.TryGetPropertyValue("error", out var errorNode) && errorNode is not null;
                                var hasResult = envObj.TryGetPropertyValue("result", out var resultNode);
                                var idMatches =
                                    envObj.TryGetPropertyValue("id", out var idNode)
                                    && idNode is System.Text.Json.Nodes.JsonValue idVal
                                    && idVal.TryGetValue<string>(out var idStr)
                                    && string.Equals(idStr, requestId, System.StringComparison.Ordinal);

                                if (resultPaths.Length > 0)
                                {
                                    // expect.result declared.
                                    if (hasError)
                                    {
                                        verdict = Platform.Engine.Abstractions.Verdict.Fail;
                                        observation = "{\"unexpectedError\":true}";
                                    }
                                    else if (!idMatches)
                                    {
                                        verdict = Platform.Engine.Abstractions.Verdict.Fail;
                                        observation = "{\"idMismatch\":true}";
                                    }
                                    else if (!hasResult)
                                    {
                                        verdict = Platform.Engine.Abstractions.Verdict.Fail;
                                        observation = "{\"malformedEnvelope\":\"missing result and error\"}";
                                    }
                                    else
                                    {
                                        string? mismatchPath = null;
                                        System.Text.Json.Nodes.JsonNode? mismatchExpected = null;
                                        System.Text.Json.Nodes.JsonNode? mismatchActual = null;

                                        for (int i = 0; i < resultPaths.Length; i++)
                                        {
                                            System.Text.Json.Nodes.JsonNode? actual = null;
                                            var hasMatch = false;
                                            try
                                            {
                                                var pathResult = Json.Path.JsonPath.Parse(resultPaths[i]).Evaluate(resultNode);
                                                var matches = pathResult.Matches;
                                                if (matches is not null && matches.Count > 0)
                                                {
                                                    actual = matches[0].Value;
                                                    hasMatch = true;
                                                }
                                            }
                                            catch (System.Exception) { hasMatch = false; }

                                            System.Text.Json.Nodes.JsonNode? expected = null;
                                            try { expected = System.Text.Json.Nodes.JsonNode.Parse(resultExpectedJson[i]); }
                                            catch (System.Exception) { expected = null; }

                                            if (!hasMatch || !System.Text.Json.Nodes.JsonNode.DeepEquals(actual, expected))
                                            {
                                                mismatchPath = resultPaths[i];
                                                mismatchExpected = expected?.DeepClone();
                                                mismatchActual = actual?.DeepClone();
                                                break;
                                            }
                                        }

                                        if (mismatchPath is not null)
                                        {
                                            verdict = Platform.Engine.Abstractions.Verdict.Fail;
                                            var diag = new System.Text.Json.Nodes.JsonObject
                                            {
                                                ["resultMismatch"] = new System.Text.Json.Nodes.JsonObject
                                                {
                                                    ["path"] = mismatchPath,
                                                    ["expected"] = mismatchExpected,
                                                    ["actual"] = mismatchActual,
                                                },
                                            };
                                            observation = diag.ToJsonString();
                                        }
                                        else
                                        {
                                            verdict = Platform.Engine.Abstractions.Verdict.Pass;
                                            observation = "{\"matched\":true}";
                                        }
                                    }
                                }
                                else if (expectedErrorCode.HasValue)
                                {
                                    // expect.error.code declared — a negative test.
                                    if (hasResult && !hasError)
                                    {
                                        verdict = Platform.Engine.Abstractions.Verdict.Fail;
                                        observation = "{\"unexpectedResult\":true}";
                                    }
                                    else if (!hasError)
                                    {
                                        verdict = Platform.Engine.Abstractions.Verdict.Fail;
                                        observation = "{\"missingError\":true}";
                                    }
                                    else
                                    {
                                        var errObj = errorNode as System.Text.Json.Nodes.JsonObject;
                                        int? actualCode = null;
                                        if (errObj is not null
                                            && errObj.TryGetPropertyValue("code", out var codeNode)
                                            && codeNode is System.Text.Json.Nodes.JsonValue codeVal
                                            && codeVal.TryGetValue<int>(out var codeInt))
                                        {
                                            actualCode = codeInt;
                                        }

                                        if (actualCode.HasValue && actualCode.Value == expectedErrorCode.Value)
                                        {
                                            verdict = Platform.Engine.Abstractions.Verdict.Pass;
                                            observation = "{\"errorCode\":" + actualCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
                                        }
                                        else
                                        {
                                            verdict = Platform.Engine.Abstractions.Verdict.Fail;
                                            var diag = new System.Text.Json.Nodes.JsonObject
                                            {
                                                ["errorCodeMismatch"] = new System.Text.Json.Nodes.JsonObject
                                                {
                                                    ["expected"] = expectedErrorCode.Value,
                                                    ["actual"] = actualCode.HasValue ? System.Text.Json.Nodes.JsonValue.Create(actualCode.Value) : null,
                                                },
                                            };
                                            observation = diag.ToJsonString();
                                        }
                                    }
                                }
                                else
                                {
                                    // Bare call: no expect declared. Pass on a clean success
                                    // envelope with a matching id; Fail when an unexpected
                                    // JSON-RPC error envelope arrives.
                                    if (hasError)
                                    {
                                        verdict = Platform.Engine.Abstractions.Verdict.Fail;
                                        observation = "{\"unexpectedError\":true}";
                                    }
                                    else if (!idMatches)
                                    {
                                        verdict = Platform.Engine.Abstractions.Verdict.Fail;
                                        observation = "{\"idMismatch\":true}";
                                    }
                                    else
                                    {
                                        verdict = Platform.Engine.Abstractions.Verdict.Pass;
                                        observation = "{\"matched\":true}";
                                    }
                                }

                                // ── engine-standard `capture:` — JSONPath against the FULL envelope.
                                // Gated on `!= Fail` (not `== Pass`) to read identically to the
                                // Core http.rest provider's own capture gate — the two are
                                // equivalent at this point in the control flow (verdict can only
                                // be Pass or Fail here), but this sample is the reference pattern
                                // contributors copy.
                                if (captureVarNames.Length > 0 && verdict != Platform.Engine.Abstractions.Verdict.Fail)
                                {
                                    var matchedFlags = new bool[captureVarNames.Length];
                                    for (int ci = 0; ci < captureVarNames.Length; ci++)
                                    {
                                        var matched = false;
                                        try
                                        {
                                            var pathResult = Json.Path.JsonPath.Parse(captureExprs[ci]).Evaluate(envObj);
                                            var matches = pathResult.Matches;
                                            if (matches is not null && matches.Count > 0 && matches[0].Value is not null)
                                            {
                                                var val = matches[0].Value!;
                                                var capturedStr = val is System.Text.Json.Nodes.JsonValue jv && jv.TryGetValue<string>(out var s)
                                                    ? s
                                                    : val.ToJsonString();
                                                vars[captureVarNames[ci]] = capturedStr;
                                                matched = true;
                                            }
                                        }
                                        catch (System.Exception) { matched = false; }

                                        matchedFlags[ci] = matched;
                                        if (!matched)
                                        {
                                            verdict = Platform.Engine.Abstractions.Verdict.Inconclusive;
                                            observation = "{\"captureUnmet\":" + System.Text.Json.JsonSerializer.Serialize(captureVarNames[ci]) + "}";
                                        }
                                    }
                                    vars[Platform.Engine.Abstractions.VarKeys.CaptureStatus(stepId)] =
                                        string.Join(",", System.Array.ConvertAll(matchedFlags, f => f ? "1" : "0"));
                                }
                            }
                        }
                    }
                }
                catch (Platform.Engine.Abstractions.Secrets.SecretResolutionException sre)
                {
                    // Missing/unknown secret = EnvironmentError (§12.1), reference-only (§17):
                    // never the value, never even the exception's own Message.
                    verdict = Platform.Engine.Abstractions.Verdict.EnvironmentError;
                    observation = "{\"secretError\":\"secret resolution failed\"" +
                        ",\"source\":" + System.Text.Json.JsonSerializer.Serialize(sre.SecretSource) +
                        ",\"path\":" + System.Text.Json.JsonSerializer.Serialize(sre.SecretPath) + "}";
                }
                catch (System.Exception ex) when (ex is System.Threading.Tasks.TaskCanceledException || ex is System.TimeoutException)
                {
                    // Timeout = Inconclusive (§12.1): the test could not complete due to a
                    // stall, not because the target responded incorrectly — mirrors http.rest.
                    verdict = Platform.Engine.Abstractions.Verdict.Inconclusive;
                    observation = "{\"timeout\":true}";
                }
                catch (System.Exception ex)
                {
                    // Connection refused / DNS failure / TLS failure / non-json-envelope /
                    // bad scheme = EnvironmentError (§12.1): a run-environment problem.
                    verdict = Platform.Engine.Abstractions.Verdict.EnvironmentError;
                    observation = "{\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.GetType().Name) + "}";
                }
                finally
                {
                    sw.Stop();
                    client.Dispose();
                }

                vars[Platform.Engine.Abstractions.VarKeys.Outcome(stepId)] =
                    new Platform.Engine.Abstractions.StepOutcome(verdict, sw.ElapsedMilliseconds, observation);
            }

            // Resolves every STRING LEAF in a parsed `params` JsonNode tree (object or
            // array, recursively — JSON-RPC 2.0 §4.2 already guarantees the ROOT is one
            // of those two shapes, enforced by JsonRpcProvider.Validate) via
            // Secret_Helpers.ResolveTemplate, mutating the tree in place. Non-string
            // leaves (numbers/bools/null) are left untouched — only a string value can
            // carry a {placeholder} or ${secret:...} token. This never template-
            // substitutes the raw JSON TEXT before parsing: a resolved value containing a
            // quote or brace would otherwise corrupt the envelope. Per-leaf assignment via
            // JsonNode is injection-safe by construction — a resolved value can only ever
            // replace one JSON string value with another, never inject structure.
            private static void ResolveParamsLeaves(
                System.Text.Json.Nodes.JsonNode? node,
                Platform.Engine.Abstractions.Secrets.ISecretAccessor secrets,
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
                else if (node is System.Text.Json.Nodes.JsonArray arr)
                {
                    for (var i = 0; i < arr.Count; i++)
                    {
                        var child = arr[i];
                        if (child is System.Text.Json.Nodes.JsonValue leaf && leaf.TryGetValue<string>(out var s))
                            arr[i] = System.Text.Json.Nodes.JsonValue.Create(Secret_Helpers.ResolveTemplate(secrets, vars, s));
                        else
                            ResolveParamsLeaves(child, secrets, vars);
                    }
                }
            }
        }
        """;

    // ── IStepCompiler<JsonRpcModel> ────────────────────────────────────────────

    /// <inheritdoc />
    public CsxFragment Emit(JsonRpcModel model, ICompileContext ctx)
    {
        var safeId = CsxFragment.SanitiseId(ctx.StepId);

        // notification + capture is a declared-incompatible combination that cannot be
        // rejected in Validate (IProjectContext carries no capture view — only
        // ICompileContext does, here).  Rather than silently ignoring the capture or
        // making a call whose result can never be captured, short-circuit to a trivial
        // block that skips the HTTP call entirely and records WHY.
        //
        // Verdict.Fail, not Inconclusive: this is an AUTHOR MISCONFIGURATION (a step
        // shape that can never do what it declares), not a timing uncertainty.
        // Inconclusive does not break CI by default (§12.1), so classifying a
        // misconfiguration that way would let the mistake hide silently.
        if (model.Notification && ctx.CaptureExprs.Count > 0)
        {
            const string reason =
                "rpc.json-rpc: 'capture' is declared on a 'notification: true' step; " +
                "a fire-and-forget notification has no response body to capture from.";
            var observationJson = "{\"error\":" + JsonSerializer.Serialize(reason) + "}";
            var observationLiteral = JsonSerializer.Serialize(observationJson);

            var shortCircuitBlock = $$"""
                {
                    Vars[Platform.Engine.Abstractions.VarKeys.Outcome({{JsonSerializer.Serialize(safeId)}})] =
                        new Platform.Engine.Abstractions.StepOutcome(
                            Platform.Engine.Abstractions.Verdict.Fail,
                            0,
                            {{observationLiteral}});
                }
                """;

            return new CsxFragment(
                RequiredUsings: s_shortCircuitUsings,
                RequiredHelpers: Array.Empty<string>(),
                StatementBlock: shortCircuitBlock);
        }

        var urlTemplateLiteral = JsonSerializer.Serialize(model.Url);
        var methodLiteral = JsonSerializer.Serialize(model.Method);
        var paramsJsonLiteral = model.ParamsJson is null ? "null" : JsonSerializer.Serialize(model.ParamsJson);
        var notificationLiteral = model.Notification ? "true" : "false";
        var requestIdLiteral = JsonSerializer.Serialize(ctx.StepId);

        var resultPaths = model.Expect?.Result?.Select(r => r.Path).ToArray() ?? Array.Empty<string>();
        var resultExpectedJson = model.Expect?.Result?.Select(r => r.ExpectedJson).ToArray() ?? Array.Empty<string>();
        var expectedErrorCodeLiteral = model.Expect?.ErrorCode is int code
            ? code.ToString(CultureInfo.InvariantCulture)
            : "null";

        string[] captureVarNames;
        string[] captureExprs;
        if (ctx.CaptureExprs is { Count: > 0 } captureMap)
        {
            captureVarNames = captureMap.Keys.ToArray();
            // This provider evaluates every capture as JSONPath (the DSL default and the
            // only extractor language a JSON-RPC envelope makes sense with); an
            // explicitly-typed `{xpath: ...}` capture is still passed through as raw
            // expression text, which will normally just fail to match against JSON — see
            // README "Known limitations".
            captureExprs = captureMap.Values.Select(c => c.Expression).ToArray();
        }
        else
        {
            captureVarNames = Array.Empty<string>();
            captureExprs = Array.Empty<string>();
        }

        var block = $$"""
            {
                await RpcJsonRpc_Helpers.ExecuteAsync(
                    Vars,
                    Secrets,
                    {{JsonSerializer.Serialize(safeId)}},
                    {{urlTemplateLiteral}},
                    {{methodLiteral}},
                    {{paramsJsonLiteral}},
                    {{notificationLiteral}},
                    {{requestIdLiteral}},
                    {{BuildStringArrayLiteral(resultPaths)}},
                    {{BuildStringArrayLiteral(resultExpectedJson)}},
                    {{expectedErrorCodeLiteral}},
                    {{BuildStringArrayLiteral(captureVarNames)}},
                    {{BuildStringArrayLiteral(captureExprs)}});
            }
            """;

        return new CsxFragment(
            RequiredUsings: s_usings,
            RequiredHelpers: new[] { HelperSource, SecretHelper.Source },
            StatementBlock: block);
    }

    /// <summary>
    /// Builds a C# array-initialiser literal from a string array, JSON-serialising
    /// each element so embedded quotes/backslashes/control characters cannot corrupt
    /// the emitted source (mirrors the Core <c>http.rest</c> provider's own helper).
    /// </summary>
    private static string BuildStringArrayLiteral(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return "new string[] { }";

        var sb = new System.Text.StringBuilder("new string[] { ");
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(JsonSerializer.Serialize(values[i]));
        }
        sb.Append(" }");
        return sb.ToString();
    }

    // ── IStepDiffRenderer ─────────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Recognises two Fail-observation shapes: <c>{"resultMismatch":{...}}</c> (an
    /// <c>expect.result</c> JSONPath assertion mismatch) and
    /// <c>{"errorCodeMismatch":{...}}</c> (an <c>expect.error</c> code mismatch).
    /// Every other shape (Pass, EnvironmentError, Inconclusive, or a different Fail
    /// reason such as <c>idMismatch</c>) is intentionally NOT renderable — there is no
    /// expected-vs-observed diff to draw.  Mirrors how the Core
    /// <c>mail-expect.smtp</c> provider implements this optional interface.
    /// </remarks>
    public bool CanRender(JsonElement observation) =>
        TryReadResultMismatch(observation, out _, out _, out _)
        || TryReadErrorCodeMismatch(observation, out _, out _);

    /// <inheritdoc cref="IStepDiffRenderer.RenderDiff" />
    public string? RenderDiff(JsonElement observation)
    {
        if (TryReadResultMismatch(observation, out var path, out var expected, out var actual))
            return RenderKeyValueTable(("path", path), ("expected", expected), ("actual", actual));

        if (TryReadErrorCodeMismatch(observation, out var expectedCode, out var actualCode))
            return RenderKeyValueTable(("expected code", expectedCode), ("actual code", actualCode));

        return null;
    }

    private static bool TryReadResultMismatch(
        JsonElement observation, out string path, out string expected, out string actual)
    {
        path = expected = actual = string.Empty;

        if (observation.ValueKind != JsonValueKind.Object
            || !observation.TryGetProperty("resultMismatch", out var diag)
            || diag.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        path = diag.TryGetProperty("path", out var p) ? p.GetRawText().Trim('"') : string.Empty;
        expected = diag.TryGetProperty("expected", out var e) ? e.GetRawText() : "null";
        actual = diag.TryGetProperty("actual", out var a) ? a.GetRawText() : "null";
        return true;
    }

    private static bool TryReadErrorCodeMismatch(
        JsonElement observation, out string expectedCode, out string actualCode)
    {
        expectedCode = actualCode = string.Empty;

        if (observation.ValueKind != JsonValueKind.Object
            || !observation.TryGetProperty("errorCodeMismatch", out var diag)
            || diag.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        expectedCode = diag.TryGetProperty("expected", out var e) ? e.GetRawText() : "null";
        actualCode = diag.TryGetProperty("actual", out var a) ? a.GetRawText() : "null";
        return true;
    }

    private static string RenderKeyValueTable(params (string Label, string Value)[] rows)
    {
        var col = rows.Max(r => r.Label.Length) + 2;
        var lines = new List<string>();
        foreach (var (label, value) in rows)
            lines.Add("  " + label.PadRight(col) + value);
        return string.Join(Environment.NewLine, lines);
    }
}
