# Project Setup and the Model

**Stage 2 of the provider authoring journey**

## Project Setup

### Create the Provider Project

Copy the template to get started:

```bash
# Navigate to your repository
cd your-provider-repo

# Copy the template and rename to YOUR organisation's prefix
# (Vouchfx.Community.* is reserved for providers hosted in the hub's
#  community/ directory — external providers use <Org>.Steps.<Name>)
cp -r template/Vouchfx.Community.Hello src/YourOrg.Steps.YourKind
cd src/YourOrg.Steps.YourKind

# Rename the namespace (via find/replace in your IDE)
# Vouchfx.Community.Hello → YourOrg.Steps.YourKind
```

### The `.csproj` File

Your provider project must:
- Reference `Vouchfx.Sdk` (use the published SDK version selected in `Directory.Build.props`) and no `Vouchfx.Engine.*` package. Add a third-party package only when your emitted code needs one: `Vouchfx.Community.JsonRpc` also references `JsonPath.Net`, and pairs it with `ICompileReferenceContributor` (see [the contract guide](provider-contract.md))
- Use a **non-reserved namespace** (never `Vouchfx.Engine.*` or `Vouchfx.Steps.*`)
- Target `net8.0`. It is the v1 provider contract target: the engine's `vouchfx providers install`
  installs only `net8.0`-compatible assets (the hub requires exactly `lib/net8.0`), and
  `Vouchfx.Sdk` is `net8.0`. It is a build target, not a runtime
  requirement, so do not raise it. Build with the .NET 10 SDK. Providers run inside the engine,
  which needs .NET 10 from the engine release that moves to it. Until that engine release, the
  published CLI is a net8.0 build; on a .NET 10-only machine install it with
  `dotnet tool install -g vouchfx --allow-roll-forward` (or set
  `DOTNET_ROLL_FORWARD=Major` for the command). `dotnet tool update` has no
  `--allow-roll-forward`, so to change version uninstall the tool, then install it again.
  Your test project should target `net10.0` (the hub's test projects do, without roll-forward)
  and can still reference the `net8.0` provider library and `Vouchfx.Sdk.Testing`.

The example targets the published Vouchfx.Sdk 1.2.0 package. Its supporting SDK
closure packages are also published on NuGet.org, so public consumers can restore this
exact version.

Here is the minimal structure:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>YourOrg.Steps.YourKind</RootNamespace>
    <LangVersion>12.0</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- Frozen v1 SDK contract — no engine dependencies needed.
         Published SDK release 1.2.0; see the project guide above. -->
    <PackageReference Include="Vouchfx.Sdk" Version="1.2.0" />
  </ItemGroup>

</Project>
```

Restore the SDK from NuGet.org at the pinned version, whose dependency closure is publicly available (see `CONTRIBUTING.md` "Building against the SDK"). For advanced use cases testing against the engine's unreleased `main` branch, see the "Building against engine main (optional)" subsection in `CONTRIBUTING.md`.

### Assembly-Graph Hygiene

The engine refuses provider DLLs that declare the reserved namespaces at startup:

- `Vouchfx.Engine.*` — reserved for engine internals
- `Vouchfx.Steps.*` — reserved for Core providers

Use your own namespace: `MyOrg.Steps.Kafka` or `Example.Steps.Hello` for externally hosted providers (the `<Org>.Steps.<Name>` convention); `Vouchfx.Community.<Name>` is the convention reserved for providers hosted in this hub's `community/` directory. This prevents collisions when multiple providers are loaded together.

**Why this matters:** Your provider DLL is loaded into the host process alongside others. If two providers both declare `Vouchfx.Steps.Foo`, the assembly-graph loader sees a collision and refuses the suite. Using a unique namespace keeps the host assembly graph clean.

### Build Warnings

This hub's root `Directory.Build.props` sets `TreatWarningsAsErrors` for every project. In your own repository, request the same strictness explicitly:

```bash
dotnet build your-provider.csproj /p:TreatWarningsAsErrors=true
```

Your provider must build with **zero warnings**. The conformance workflow enforces this for all hub submissions.

## The Model — Strongly-Typed Step Shape

Your step model is an immutable `record` implementing `IStepModel`. It represents the YAML shape the test author writes.

### Example: JsonRpc Model

From the first community provider, `Vouchfx.Community.JsonRpc`:

```csharp
using Vouchfx.Sdk;

namespace Vouchfx.Community.JsonRpc;

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

---

**Next:** [The Contract Surfaces](provider-contract.md) — the four mandatory interfaces and three optional extension interfaces your provider implements.
