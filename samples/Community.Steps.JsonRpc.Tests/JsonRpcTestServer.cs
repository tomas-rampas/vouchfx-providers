// Community.Steps.JsonRpc.Tests — JsonRpcTestServer.
//
// Self-hosts a minimal JSON-RPC 2.0 responder on loopback using System.Net.HttpListener
// (BCL-only, no Docker, no WireMock). Mirrors the engine's own Docker-free HTTP test
// pattern verified in the engine repo at
// tests/Platform.Engine.Compilation.Tests/HttpRestExecutionTests.cs:
//   • HttpListener prefixes cannot bind port 0, so a free port is discovered by binding
//     a TcpListener to port 0, reading the OS-assigned port, then releasing it
//     (FindFreePort below — copied verbatim from that engine test).
//   • A closed port (reserved, then released, with no listener ever started on it) is
//     used to simulate connection-refused, driving the EnvironmentError test.
//
// Canned handlers:
//   sum    — params {a, b} -> result a + b                    (valid result)
//   echo   — result echoes params back                        (valid result)
//   flaky  — returns a NOT-yet-converged result for the first (FlakyConvergeAfterCalls
//            - 1) calls, then converges — drives the verifyMode: RETRY test.
//   (anything else) -> JSON-RPC error -32601 "Method not found" (unknown method)
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Community.Steps.JsonRpc.Tests;

/// <summary>
/// A minimal, in-process JSON-RPC 2.0 HTTP responder used by the conformance tests.
/// </summary>
/// <remarks>
/// Public (not internal): xUnit's <c>IClassFixture&lt;T&gt;</c> requires the fixture
/// type to be at least as accessible as the public test class that consumes it.
/// </remarks>
public sealed class JsonRpcTestServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private int _flakyCallCount;

    /// <summary>The base URL (including path) the tests should POST JSON-RPC requests to.</summary>
    public string BaseUrl { get; }

    /// <summary>How many calls to "flaky" are required before it converges to "converged".</summary>
    public int FlakyConvergeAfterCalls { get; set; } = 3;

    public JsonRpcTestServer()
    {
        var port = FindFreePort();
        BaseUrl = $"http://localhost:{port}/rpc";

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>Resets the "flaky" call counter — call at the top of a test that relies on it.</summary>
    public void ResetFlaky() => Interlocked.Exchange(ref _flakyCallCount, 0);

    /// <summary>
    /// Finds a free loopback TCP port by binding to port 0 and reading the assigned
    /// port number before releasing the socket (HttpListener prefixes require a
    /// literal, non-zero port).
    /// </summary>
    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Reserves a free loopback port and immediately releases it WITHOUT starting any
    /// listener on it — a POST to the returned URL fails with connection-refused,
    /// simulating an unreachable JSON-RPC endpoint for the EnvironmentError test.
    /// </summary>
    public static string ReserveClosedPortUrl()
    {
        var port = FindFreePort();
        return $"http://localhost:{port}/rpc";
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext? ctx;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                break; // listener stopped
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            var request = JsonNode.Parse(body) as JsonObject;
            var method = request?["method"]?.GetValue<string>() ?? string.Empty;
            var idNode = request?["id"];
            var isNotification = idNode is null;

            byte[]? responseBytes = null;
            if (!isNotification)
            {
                var envelope = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = idNode?.DeepClone(),
                };

                switch (method)
                {
                    case "sum":
                        var a = request!["params"]?["a"]?.GetValue<double>() ?? 0d;
                        var b = request["params"]?["b"]?.GetValue<double>() ?? 0d;
                        // Nested under "sum" (not a bare number) so tests can exercise a
                        // realistic JSONPath assertion ("$.sum") rather than "$".
                        envelope["result"] = new JsonObject { ["sum"] = a + b };
                        break;

                    case "echo":
                        envelope["result"] = request!["params"]?.DeepClone();
                        break;

                    case "flaky":
                        var call = Interlocked.Increment(ref _flakyCallCount);
                        envelope["result"] = call >= FlakyConvergeAfterCalls ? "converged" : "not-yet";
                        break;

                    default:
                        envelope["error"] = new JsonObject
                        {
                            ["code"] = -32601,
                            ["message"] = "Method not found",
                        };
                        break;
                }

                responseBytes = Encoding.UTF8.GetBytes(envelope.ToJsonString());
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            if (responseBytes is null)
            {
                ctx.Response.ContentLength64 = 0;
            }
            else
            {
                ctx.Response.ContentLength64 = responseBytes.Length;
                await ctx.Response.OutputStream.WriteAsync(responseBytes).ConfigureAwait(false);
            }

            ctx.Response.Close();
        }
        catch (Exception)
        {
            try
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.Close();
            }
            catch (Exception)
            {
                // best-effort — the client-side test will see a transport failure either way.
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch (Exception) { /* already stopped */ }
        try { _listener.Close(); } catch (Exception) { /* already closed */ }
        _cts.Dispose();
    }
}

/// <summary>
/// xUnit class fixture wrapping a single <see cref="JsonRpcTestServer"/> shared across
/// all tests in a test class (started once, disposed once).
/// </summary>
public sealed class JsonRpcTestServerFixture : IDisposable
{
    public JsonRpcTestServer Server { get; } = new();

    public void Dispose() => Server.Dispose();
}
