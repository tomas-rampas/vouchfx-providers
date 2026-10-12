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

The canonical worked example is the [`ledger-jsonrpc`](https://samples.vouchfx.io/samples/ledger-jsonrpc/README.html) sample application in vouchfx-samples. It consumes [`Vouchfx.Community.JsonRpc`](https://www.nuget.org/packages/Vouchfx.Community.JsonRpc) (1.0.0-alpha.1), the first published community provider.

The SDK reference below targets the frozen v1 release; public restore requires its SDK
closure to be available on NuGet.org. The Community provider keeps its independent
`1.0.0-alpha.1` version. An SDK repin does not publish or promote a provider package.

Add the provider to your application's `.csproj`:

```xml
<ItemGroup>
  <!-- Your vouchfx engine host + SDK closure -->
  <PackageReference Include="Vouchfx.Sdk" Version="1.0.0" />
  
  <!-- The community provider you want to use -->
  <PackageReference Include="Vouchfx.Community.JsonRpc" Version="1.0.0-alpha.1" />
</ItemGroup>
```

### Step 2: Pre-Release Pinning

Community providers are versioned independently of the SDK. The SDK release target is stable, while this example still consumes the pre-release JsonRpc provider. NuGet enforces the dependency rule: a **stable** package may not depend on a pre-release one — packing such a graph raises warning NU5104, so anything that depends on a pre-release provider must itself be versioned pre-release. When you pack your application or a custom provider:

```bash
dotnet pack MyApp.csproj -c Release
# Warning NU5104: The dependency Vouchfx.Community.JsonRpc 1.0.0-alpha.1 uses
# prerelease, but the package MyApp does not mark itself as prerelease.
```

**Fix:** Mark the package you are packing as pre-release to avoid NU5104. Pin compatible SDK and provider versions independently; their pre-release labels do not need to match. Add a `<Version>` to your project:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <Version>1.0.0-alpha.1</Version>  <!-- Mark this package as pre-release -->
</PropertyGroup>
```

A stable package can use this dependency graph once all of its dependencies are stable releases.

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

For a real implementation, see the samples walkthrough (Path B) which shows the complete runner pattern with AST parsing and discovery.

### Step 4: Publishing Cadence and Registry

Hub-hosted Community providers have their own release cadence. Adopting a stable SDK
pin does not require a stable Community provider release. Before cutting a provider
release tag, its SDK dependency pin must be publicly restorable from NuGet.org; the
publish workflow checks this dependency boundary. Check `registry/community-providers.json`
in the hub for the current registry of published providers.

The tag scheme for hub-hosted providers is `<Provider>/vX.Y.Z` — for example, `Vouchfx.Community.JsonRpc/v1.0.0-alpha.1`.

### Updating a Provider

To update to a newer version:

1. Bump the version pin in your `.csproj`
2. Check the provider's `MinEngineVersion` metadata — it declares the earliest engine version it supports
3. If the provider's `MinEngineVersion` is newer than your pinned engine SDK, you must also bump the SDK

Example: if you are on SDK v1.0.0-alpha.3 and want to use `Vouchfx.Community.JsonRpc v1.0.0-alpha.2`, and that provider declares `MinEngineVersion: 1.0.0-alpha.4`, then you must also bump to `Vouchfx.Sdk v1.0.0-alpha.4` or later.

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

The [`ledger-jsonrpc`](https://samples.vouchfx.io/samples/ledger-jsonrpc/README.html) sample application demonstrates both paths:

- **NuGet path:** `runner/LedgerRunner.csproj` references `Vouchfx.Community.JsonRpc` as a NuGet package
- **Source path:** The samples site's [custom-runner walkthrough](https://samples.vouchfx.io/docs/custom-runner.html) documents the complete end-to-end walkthrough of building a runner, composing the step registry, parsing suites, and handling exit codes

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
