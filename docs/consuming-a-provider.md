# Consuming a Provider

**Using Community and Core providers in your test application**

This page explains how to add a published provider to your vouchfx test application. It covers both the NuGet package path and building a provider from source.

## Why No Runtime Loader?

vouchfx providers are **compile-time, source-level plugins** — not dynamically loaded at runtime. This is a deliberate design choice:

- **Type safety:** The engine reflects over your loaded provider assemblies to discover step kinds at startup. Strong typing and compile-time binding mean fewer surprises.
- **Simplicity:** No plugin registry, no sandbox, no version-negotiation complexity. A provider is just a C# class.
- **Predictability:** Your application's set of available step types is fixed at compile time. There is no loading uncertainty.

The tradeoff is that you cannot install a provider without rebuilding your application. This is acceptable because providers ship as NuGet packages, and your CI/CD already rebuilds on dependency changes.

## Path A: Consuming from NuGet

### Step 1: Add the NuGet Reference

The canonical worked example is the [`ledger-jsonrpc`](https://samples.vouchfx.io/samples/ledger-jsonrpc/README.html) sample application in vouchfx-samples. It consumes the published [`Vouchfx.Community.JsonRpc` 1.0.0](https://www.nuget.org/packages/Vouchfx.Community.JsonRpc/1.0.0) package.

`Vouchfx.Sdk` and `Vouchfx.Sdk.Testing` `1.0.5` and their dependency closure are
published on NuGet.org. The provider Release build, Hello tests (5/5), JsonRpc tests
(24/24), and formatting check passed against this SDK version.
The current Hello and JsonRpc source providers also passed a two-step suite against
Core 1.0.5. A copied Hello template restored outside the repository and passed 5/5
consumer tests against the published SDK 1.0.5 packages.
The already-published JsonRpc 1.0.0 package keeps its original package and registry
metadata. The current hub source has been updated for SDK 1.0.5 and declares a 1.0.5
engine floor; that source change does not alter the immutable 1.0.0 package. Do not
assume the earlier package metadata describes binaries rebuilt from this source.
SDK and Community provider versions remain independent; an SDK repin does not
publish or promote a provider package. The sample runner builds its engine and SDK
from the source revision in `ENGINE_PIN`, while restoring JsonRpc from NuGet.
For your own SDK consumer, use the package references below.

Add the provider to your application's `.csproj`:

```xml
<ItemGroup>
  <!-- SDK contract for your custom runner -->
  <PackageReference Include="Vouchfx.Sdk" Version="[1.0.5]" />

  <!-- The community provider you want to use -->
  <PackageReference Include="Vouchfx.Community.JsonRpc" Version="[1.0.0]" />
</ItemGroup>
```

### Step 2: Pin Versions and Record the Resolved Graph

Use exact NuGet ranges such as `[1.0.0]`; a bare `Version="1.0.0"` is a minimum
version requirement. Record the resolved dependency graph in `packages.lock.json`
and regenerate it when you intentionally change a pin. A lock file records versions
and integrity hashes; enforcing it requires locked-mode restore with a lock generated
for the target platform. The ledger runner records its graph without enforcing locked
mode because its Aspire runtime assets differ across operating systems.

The SDK and JsonRpc references above are stable releases. They do not require your
own package to have a pre-release version.

#### If You Choose a Pre-Release Dependency

NuGet warning NU5104 applies when a stable package depends on a pre-release package.
For example, if you choose an independently versioned `Org.Steps.YourKind` preview
dependency, the package you pack must also be marked pre-release:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <Version>2.0.0-preview.1</Version>
</PropertyGroup>
```

Choose compatible SDK and provider versions independently; their pre-release labels
do not need to match. This rule applies only when a dependency is pre-release.

### Step 3: Discovery and initialisation

The engine's `StepKindRegistry` reflects over the provider assemblies explicitly supplied by your custom runner, then freezes the step-kind map. Adding a package or project reference alone does not register its provider.

For a suite containing only JSON-RPC steps, supply the community assembly:

```csharp
using Vouchfx.Community.JsonRpc;
using Vouchfx.Sdk;

var registry = StepKindRegistry.BuildAndFreeze(new[]
{
    typeof(JsonRpcProvider).Assembly,
});
```

For mixed suites, include the required Core provider assemblies in the same list. The [ledger-jsonrpc custom-runner walkthrough](https://samples.vouchfx.io/docs/custom-runner.html) shows `ProviderAssemblies()` containing its four Core assemblies and `typeof(JsonRpcProvider).Assembly`. Pass that list to `StepKindRegistry.BuildAndFreeze` before executing parsed scenarios.

For a complete runner implementation with AST parsing and discovery, follow the samples custom-runner walkthrough linked above.

### Step 4: Publishing Cadence and Registry

Hub-hosted Community providers have their own release cadence. Adopting a stable SDK
pin does not require a stable Community provider release. Before cutting a provider
release tag, its SDK dependency pin must be publicly restorable from NuGet.org; the
publish workflow checks this dependency boundary. Check `registry/community-providers.json`
in the hub for the current registry of published providers.

The tag scheme for hub-hosted providers is `<Provider>/vX.Y.Z` — for example,
`Vouchfx.Community.JsonRpc/v1.0.0`. The earlier `1.0.0-alpha.1` release is historical;
it remains a separately versioned package, not the dependency used by the stable example above.

### Updating a Provider

To update to a newer version:

1. Bump the version pin in your `.csproj`
2. Check the provider's `MinEngineVersion` metadata — it declares the earliest engine version it supports
3. If the provider source was compiled against SDK 1.0.5, use Core 1.0.5 or newer: the 1.0.5 SDK assembly identity cannot load in Core 1.0.0. Keep the runner's Core and SDK references on compatible versions.

Regenerate the lock file after changing references, and validate your suites against
the selected engine and provider versions before adopting the update.

## Path B: Building from Source

For unpublished or hub-hosted providers without a published NuGet package, consume via source-level build.

### Hub-Hosted Providers

Clone the vouchfx-providers repository and reference the provider project directly:

```bash
git clone https://github.com/tomas-rampas/vouchfx-providers.git
cd your-application
```

Add the provider project to your solution:

```xml
<ItemGroup>
  <ProjectReference Include="../vouchfx-providers/community/Vouchfx.Community.JsonRpc/Vouchfx.Community.JsonRpc.csproj" />
</ItemGroup>
```

Build your application, then include `typeof(JsonRpcProvider).Assembly` in the assembly list supplied to `StepKindRegistry.BuildAndFreeze`, alongside any required Core providers.

### External Repositories

For a provider hosted in its own repository:

```bash
git clone https://github.com/org/their-provider.git
cd your-application
```

Add it the same way:

```xml
<ItemGroup>
  <ProjectReference Include="../their-provider/src/Org.Steps.YourKind/Org.Steps.YourKind.csproj" />
</ItemGroup>
```

### The Canonical Worked Example

The [`ledger-jsonrpc`](https://samples.vouchfx.io/samples/ledger-jsonrpc/README.html) sample shows how to combine a published Community provider with a source-built engine:

- **NuGet dependency:** `runner/LedgerRunner.csproj` pins `Vouchfx.Community.JsonRpc` to `[1.0.0]`.
- **Engine source build:** The runner uses project references into the checkout selected by `ENGINE_PIN` for its engine, SDK and Core providers. Its SDK therefore has a local source-build identity. Follow the [custom-runner walkthrough](https://samples.vouchfx.io/docs/custom-runner.html) to build that runner, compose its step registry, parse suites and handle exit codes.

The runner shows:

- `StepKindRegistry.BuildAndFreeze(ProviderAssemblies())` — runtime reflection and validation over the explicitly supplied provider assemblies
- `ScenarioRunner.RunSuiteAsync()` — parse, orchestrate, execute, collect verdicts
- `Inconclusive` capture-verdict handling — how to detect when a prior step's capture was unmet
- Verdict folding and exit-code mapping — translating engine verdicts to CI exit codes

**This is the reference implementation.** Refer to it when building your own runner.

## Future: `vouchfx providers install`

A planned feature on the engine roadmap is a CLI command for automatic provider installation:

```bash
vouchfx providers install Vouchfx.Community.JsonRpc
```

This would automate the pin-and-rebuild flow. Until then, use the NuGet package or source reference paths above.

---

**Next:** Learn how to write a provider. Start with the [provider authoring journey](implementing-a-provider.md) and the [ledger-jsonrpc walkthrough](https://samples.vouchfx.io/samples/ledger-jsonrpc/README.html).

**Community registry:** [the community provider registry](../registry/README.md) — browse the published providers.
