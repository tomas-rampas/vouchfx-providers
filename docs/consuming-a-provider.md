# Consuming a Provider

**Using Community and Core providers in your test application**

This page explains how to add a published provider to your vouchfx test application. It covers three routes: installing it with the engine CLI (Path C), referencing the NuGet package from a custom runner (Path A), and building a provider from source (Path B). If you run your suites with the stock `vouchfx` CLI, start with [Path C](#path-c-installing-with-the-engine-cli).

## How Providers Are Loaded

vouchfx providers are **compile-time, source-level plugins** — the engine discovers them from the provider assemblies a host supplies, then freezes the step-kind map at startup. This is a deliberate design choice:

- **Type safety:** The engine reflects over your loaded provider assemblies to discover step kinds at startup. Strong typing and compile-time binding mean fewer surprises.
- **Simplicity:** No sandbox and no version-negotiation complexity beyond a declared minimum engine version. A provider is just a C# class, and it runs as trusted code in the host process.
- **Predictability:** The set of available step types is fixed before the first step runs: at compile time for a custom runner, at startup for the CLI. There is no loading uncertainty.

What changed in engine 1.2.0 is who supplies the assemblies. The stock `vouchfx` CLI now loads trusted provider assemblies from directories at startup, and `vouchfx providers install` can fetch a published provider into one (Path C), so you can add a provider without rebuilding anything. A custom runner still references the provider as a NuGet package or project and passes its assembly to the registry explicitly (Paths A and B); that remains the route when you own the host application.

## Path A: Consuming from NuGet

### Step 1: Add the NuGet Reference

The canonical worked example is the [`ledger-jsonrpc`](https://samples.vouchfx.io/samples/ledger-jsonrpc/README.html) sample application in vouchfx-samples. It consumes the published [`Vouchfx.Community.JsonRpc` 1.0.0](https://www.nuget.org/packages/Vouchfx.Community.JsonRpc/1.0.0) package.

`Vouchfx.Sdk` and `Vouchfx.Sdk.Testing` `1.2.0` and their dependency closure are
published on NuGet.org. The provider Release build, Hello tests (5/5), JsonRpc tests
(24/24), and formatting check passed against this SDK version.
The current Hello and JsonRpc source providers also passed a two-step suite on the stock
Core 1.2.0 CLI, which loaded them from a `--providers` directory. A copied Hello template
restored outside the repository and passed 5/5 consumer tests against the published SDK
1.2.0 packages.
The already-published JsonRpc 1.0.0 package keeps its original package and registry
metadata. The current hub source has been updated for SDK 1.2.0 and declares a 1.2.0
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
  <PackageReference Include="Vouchfx.Sdk" Version="[1.2.0]" />

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
3. If the provider source was compiled against SDK 1.2.0, use Core 1.2.0 or newer: the 1.2.0 SDK assembly identity cannot load in Core 1.0.0. Keep the runner's Core and SDK references on compatible versions.

Regenerate the lock file after changing references, and validate your suites against
the selected engine and provider versions before adopting the update.

If you installed the provider with the CLI instead (Path C), re-run
`vouchfx providers install` with the new `--version`: the install replaces the previous
package directory in one transaction, and a failure before promotion restores the old version.

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

## Path C: Installing with the engine CLI

Since engine 1.2.0, the stock `vouchfx` CLI can fetch a published provider and load it at startup, so a project that only runs `.e2e.yaml` suites needs neither a custom runner nor a rebuild. The engine's [external providers guide](https://vouchfx.io/external-providers/) is the authority for the commands and their flags; this section covers what is specific to hub providers.

```bash
# By Community registry step kind
vouchfx providers install rpc.json-rpc

# By exact NuGet package ID, pinning the version
vouchfx providers install Vouchfx.Community.JsonRpc --version 1.0.0

vouchfx providers list
vouchfx providers uninstall Vouchfx.Community.JsonRpc
```

- **Step kind or package ID.** An argument shaped like a step kind (`family.provider`, lowercase) is looked up in this hub's [registry](../registry/README.md) first, and the matching entry's `nuget` field names the package to install. Any other argument is treated as an exact NuGet package ID. A registry entry without a `nuget` field, such as hub-hosted source that has not been published, cannot be installed by step kind; build it from source instead (Path B).
- **Version.** Without `--version`, the CLI installs the package's latest listed stable version. The Vouched badge attests one specific version of a provider (`vouchedVersion` in the registry) and the installer does not consult it, so pin `--version` when you want the reviewed version.
- **Location and use.** Packages install to `~/.vouchfx/providers/<lowercase-package-id>/` by default, and `--directory` overrides that root. `vouchfx list` shows the step kinds the CLI has registered, and `watch` sessions must be restarted after an install, update or removal. For repeatable CI runs, install into a directory you control and pass it to `vouchfx run` with `--providers` and `--no-default-providers`, with `VOUCHFX_PROVIDERS` unset.
- **Feed.** NuGet.org by default. `--source` selects another HTTPS NuGet v3 feed or a local feed directory, and only that source is consulted for the package and its dependencies, so it must carry both. The registry carries no feed information, so a provider hosted elsewhere needs `--source` and its exact package ID.
- **Trust.** A provider runs as trusted code inside the CLI process, with no sandbox. The installer records SHA-256 hashes of what it downloads but does not verify package signatures, and a registry listing is not an endorsement: only the Vouched badge is. Read a provider's source and README before installing it.

The installed provider must still satisfy its `MinEngineVersion`; the CLI checks this at startup, before the provider contributes anything.

---

**Next:** Learn how to write a provider. Start with the [provider authoring journey](implementing-a-provider.md) and the [ledger-jsonrpc walkthrough](https://samples.vouchfx.io/samples/ledger-jsonrpc/README.html).

**Community registry:** [the community provider registry](../registry/README.md) — browse the published providers.
