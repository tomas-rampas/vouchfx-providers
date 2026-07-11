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

The canonical worked example is the [`ledger-jsonrpc`](https://github.com/tomas-rampas/vouchfx-samples/tree/main/samples/ledger-jsonrpc) sample application in vouchfx-samples. It consumes [`Vouchfx.Community.JsonRpc`](https://www.nuget.org/packages/Vouchfx.Community.JsonRpc) (1.0.0-alpha.1), the first published community provider.

Add the provider to your application's `.csproj`:

```xml
<ItemGroup>
  <!-- Your vouchfx engine host + SDK closure -->
  <PackageReference Include="Vouchfx.Sdk" Version="1.0.0-alpha.4" />
  
  <!-- The community provider you want to use -->
  <PackageReference Include="Vouchfx.Community.JsonRpc" Version="1.0.0-alpha.1" />
</ItemGroup>
```

### Step 2: Pre-Release Pinning

Community providers are pre-release while the engine SDK is pre-release (before v1.0.0 GA). NuGet enforces the compatibility rule: a package marked `1.0.0-alpha.4` can only depend on other pre-release packages (NU5104 warning). When you pack your application or a custom provider:

```bash
dotnet pack MyApp.csproj -c Release
# Warning NU5104: The dependency Vouchfx.Community.JsonRpc 1.0.0-alpha.1 uses
# prerelease, but the package MyApp does not mark itself as prerelease.
```

**Fix:** Pin both your application and all community-provider dependencies to the same pre-release designation. Add a `<Version>` to your project:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <Version>1.0.0-alpha.1</Version>  <!-- Mark as pre-release, matching the SDK pin -->
</PropertyGroup>
```

Once the engine reaches v1.0.0 GA and community providers do the same, this constraint disappears.

### Step 3: Discovery and Initialization

The engine's `StepKindRegistry` reflects over all loaded provider assemblies at suite startup and builds its step-kind map. Your provider is automatically discovered — no manual registration needed.

In your custom runner (see the [ledger-jsonrpc custom-runner walkthrough](https://github.com/tomas-rampas/vouchfx-samples/blob/main/samples/ledger-jsonrpc/docs/custom-runner.md)), the engine discovers providers as it loads your application's assembly graph:

```csharp
// The engine's ScenarioRunner discovers providers reflectively from loaded assemblies.
// As long as Vouchfx.Community.JsonRpc is in your dependencies and loaded,
// the rpc.json-rpc step kind is available.
var result = await ScenarioRunner.RunSuiteAsync(
    suiteSource: "path/to/suite.e2e.yaml",
    /* ... */
);
```

### Step 4: Publishing Cadence and Registry

The hub publishes community providers independently on the regular engine release schedule:

- Engine v1.0.0 released → pinned SDK version published
- Community provider v1.0.0 released → compatible with SDK v1.0.0 (or later minors)
- Check `registry/community-providers.json` in the hub for the current registry of published providers

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

Build your application. The provider is compiled into your assembly graph and discovered at runtime.

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

The [`ledger-jsonrpc`](https://github.com/tomas-rampas/vouchfx-samples/tree/main/samples/ledger-jsonrpc) sample application demonstrates both paths:

- **NuGet path:** `ledger-jsonrpc.csproj` references `Vouchfx.Community.JsonRpc` as a NuGet package
- **Source path:** The sample's own custom runner is documented in `docs/custom-runner.md` — read it for a complete end-to-end walkthrough of building a runner, composing the step registry, parsing suites, and handling exit codes

The runner shows:

- `StepKindRegistry.BuildAndFreeze()` — compile-time step kind discovery and validation
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

**Next:** Learn how to write a provider. Start with the [provider authoring journey](implementing-a-provider.md) and the [ledger-jsonrpc walkthrough](https://github.com/tomas-rampas/vouchfx-samples/tree/main/samples/ledger-jsonrpc).

**Community registry:** [registry/community-providers.json](../registry/README.md) — browse the published providers.
