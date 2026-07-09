# Contributing a Provider to vouchfx

This guide explains how to submit a Community provider for listing in the index and how to work towards the Vouched badge.

## Before You Start

**You should have already written your provider.** This repository is for *listing* and *submitting* providers, not for authoring them. To learn how to write a provider, see:

- **Engine [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md)** — complete guide with the step type model, the provider contract (four required interfaces), hard rules for CsxFragment composition, reserved namespaces, and testing patterns.
- **[`Example.Steps.Echo`](https://github.com/tomas-rampas/vouchfx/tree/main/examples/Example.Steps.Echo)** — a fully documented worked example with friction log.
- **[`Example.Steps.Hello`](https://github.com/tomas-rampas/vouchfx/tree/main/examples/Example.Steps.Hello)** — a minimal copyable template.

## Provider Requirements

All providers in this repository must meet these baseline requirements:

### Licensing and Attribution
- **Apache-2.0 license** (or compatible). The vouchfx engine and all Core providers are Apache-2.0; all community providers must be compatible.
- **Developer Certificate of Origin (DCO) sign-off.** Commits must be signed off with `git commit -s` or the GitHub web UI "Sign off" checkbox. The DCO confirms you have the right to license your work. It is lighter-weight than a Contributor Licence Agreement and is industry standard.

### The Provider Contract
- Your provider must implement the four required interfaces from `Platform.Sdk`: `IStepProvider`, `IStepBinder<TModel>`, `IStepValidator<TModel>`, and `IStepCompiler<TModel>`.
- Your model must be a strongly-typed record (never `Dictionary<string,object>`).
- You must NOT use the reserved namespace prefixes: `Platform.Engine.*` or `Platform.Steps.*`. Use your own namespace (e.g. `MyOrg.Steps.Kafka`, `Community.Steps.Snowflake`).
- Your provider must be discoverable at suite startup via the reflective `StepKindRegistry`. Add the `[StepProvider]` attribute to your provider class; no manual registration is required.

### CsxFragment Composition
When your `IStepCompiler<TModel>` emits a `CsxFragment`, you must follow the strict rules in the engine's architecture blueprint (§13.3.1):

1. **Three fields only:** `RequiredUsings` (bare namespace strings), `RequiredHelpers` (nested static classes prefixed with your provider id), `StatementBlock` (brace-enclosed C# block).
2. **No `using var` in the Roslyn script body.** Use plain `var` + explicit `.Dispose()` in a `finally`.
3. **Emit bodies as C# 11 double-dollar raw strings** (`$$"""…"""`). Single `{`/`}` are literal braces; `{{hole}}` is an interpolation.
4. **Sanitise step ids before splicing.** Call `CsxFragment.SanitiseId(stepId)` to convert hyphens to underscores.
5. **Cross-step state passes only through `Vars`.** Read earlier steps' captured state from `Vars`; write your outcome via `VarKeys.Outcome(safeId)`.

See the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) for worked code examples and the blueprint (§13.3.1) for full details.

### Testing
- You must have a test suite that exercises your provider. Use the `Platform.Sdk.Testing` package:
  - For dependency-free providers: use `ProviderTestHarness.RunSingleStepAsync()` for end-to-end unit tests (no Docker needed).
  - For infrastructure providers: author an integration-test fixture using the vouchfx engine's topology orchestration.
- Local tests must pass: `dotnet test <your-provider.Tests> -c Release --filter "requires!=docker"`.
- If your provider uses infrastructure (databases, brokers), include a Docker integration test. Community providers can author this within their own repository or contribute it to the main vouchfx repository if it uses Core infrastructure.

## One Submission Flow, Two Hosting Options

All Community-tier providers follow one contribution flow with two hosting choices:

**Option A — external hosting (your repository + NuGet).** Your provider is authored, tested, and published on NuGet:

1. **Publish to NuGet** — pack your provider as a NuGet package and push it to nuget.org.
2. **Open an issue** — click [**New Issue → Provider Listing**](.github/ISSUE_TEMPLATE/provider-listing.yml) and fill in the form; a maintainer adds your entry. Or:
3. **Submit an entry PR** — fork this repository, add an entry to `registry/community-providers.json` following the schema in `registry/community-providers.schema.json`, and open a pull request. See [`registry/README.md`](registry/README.md) for the field meanings and validation.

**Option B — hub hosting (source PR into `community/`, no NuGet account needed).** Contribute the provider source itself:

1. Start from [`template/`](template/) (`Community.Steps.Hello` + its tests) or model on [`community/Community.Steps.JsonRpc`](community/Community.Steps.JsonRpc/), the hub's worked reference.
2. Name your projects `community/<YourProvider>/` + `community/<YourProvider>.Tests/`; use a non-reserved namespace (the `Community.Steps.<Name>` convention is recommended — never `Platform.Engine.*`/`Platform.Steps.*`).
3. Build standalone against the packed SDK (`packages-local/` feed via `nuget.config`; see "Building Before v1.0 GA" below). Your projects do **not** need to join the `.sln` — CI discovers `community/**/*.Tests.csproj` by glob and runs each submission in its own step. Every public member of your provider must have an XML-doc comment (CS1591 enforced by the repo's TreatWarningsAsErrors setting); the reference provider [`Community.Steps.JsonRpc`](community/Community.Steps.JsonRpc) demonstrates this quality bar. When you publish to NuGet, the pack gate validates per-provider metadata: `Description` (not the MSBuild default), `PackageTags`, `PackageReadmeFile` + a packaged `README.md` file; `PackageId`, repository URL, and Apache-2.0 licence expression are set automatically from `community/Directory.Build.props`.
4. Add your registry entry with `"hosting": "hub"`. If your provider will be published to NuGet (recommended for discoverability), set `nuget` to the provider directory name (e.g. `nuget: "Community.Steps.JsonRpc"` for `community/Community.Steps.JsonRpc`); the publish workflow requires this field to cut a release tag (`<Provider>/vX.Y.Z`). **Release timing:** no `<Provider>/vX.Y.Z` tag may be cut until your provider's `Platform.Sdk` dependency pin is publicly restorable from NuGet.org. The publish workflow enforces this with a dependency-resolvability preflight check — publishing an unrestorable package would burn an immutable version number.
5. Open the PR using the [community submission template](.github/PULL_REQUEST_TEMPLATE/community-submission.md), with every commit DCO-signed (`git commit -s`).

The merge bar for Option B is **hygiene, not review**: Apache-2.0 licence, DCO, namespace rules, no step-kind collision, and a green conformance lane. **Hosting in this repository is not endorsement** — your provider's README must open with the Community-tier notice, and you remain the owner of your folder (a CODEOWNERS line is added at merge). The published Vouched rubric is the feedback for what is needed to work towards the Vouched badge.

## Building Before v1.0 GA

The `Platform.Sdk` and `Platform.Sdk.Testing` packages are published to NuGet with the vouchfx v1.0 **GA** release. The v1.0.0-alpha pre-releases include only the vouchfx CLI tool; the SDK packages are not yet on NuGet. Until the GA release, pack the five SDK-closure projects locally from the engine:

```bash
# From the vouchfx-providers repo root, with the engine checked out at <engine>:
for p in \
  src/Engine/Platform.Engine.Abstractions/Platform.Engine.Abstractions.csproj \
  src/Engine/Platform.Engine.Authoring/Platform.Engine.Authoring.csproj \
  src/Engine/Platform.Engine.Compilation/Platform.Engine.Compilation.csproj \
  src/Sdk/Platform.Sdk/Platform.Sdk.csproj \
  src/Sdk/Platform.Sdk.Testing/Platform.Sdk.Testing.csproj ; do
  dotnet pack "<engine>/$p" -c Release -o packages-local
done
```

This repository's `nuget.config` already points to `packages-local`:

```xml
<add key="local" value="packages-local" />
```

Local builds will consume the locally packed SDK.

## Earning the Vouched Badge

After your Community provider is listed in the registry (via Option A or Option B above), you can work towards the optional **Vouched badge** — an optional registry metadata flag (`"vouched": true`) awarded by a maintainer after reviewing your provider against the published rubric.

**How to request the badge:**

1. **Your provider is already listed** — the provider is in the registry and CI is passing (if hub-hosted).

2. **Open a Vouched request issue** — click [**New Issue → Vouched Request**](.github/ISSUE_TEMPLATE/vouched-request.yml) linking to your provider source and confirming which rubric items are met. This proposes the provider for review.

3. **Maintainer review** — a maintainer reviews your provider against the [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md) rubric:
   - Integration-test fixture passes on engine main + two preceding minors
   - README with ≥3 use cases + known-limitations section
   - Security sign-off: credentials, transitive CVEs, TLS, no telemetry, signature
   - Apache-2.0 + DCO sign-off
   - MinEngineVersion declared
   - CSX reviewed for §13.3.1 conformance by a maintainer

4. **Badge award** — upon approval, a maintainer opens a one-line registry PR adding `"vouched": true`. Once merged, the badge is live on your listing. The badge does not move your provider to a new tier or hosted location — it remains where it is, now with platform-team endorsement.

## The Vouched Rubric (Full Reference)

For the complete checklist, see [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md). Summary:

1. Integration-test fixture passes on main + two preceding minors
2. README with ≥3 use cases + known-limitations section
3. Security sign-off: credentials, transitive CVEs, TLS, no telemetry, signature
4. Apache-2.0 + DCO sign-off
5. MinEngineVersion declared
6. CSX reviewed for §13.3.1 conformance by a maintainer

## Namespace Rules

**Reserved prefixes (you cannot use these):**
- `Platform.Engine.*` — reserved for vouchfx engine code
- `Platform.Steps.*` — reserved for Core providers

**Your provider's namespace must be unique.** Examples:
- `MyOrg.Steps.Kafka`
- `Community.Steps.Snowflake`
- `Example.Steps.Hello`

A unique namespace prevents assembly-graph collisions and makes it clear which team owns the code.

## Questions?

- **How do I write a provider?** See the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) and the worked examples.
- **What is CsxFragment composition?** See the architecture blueprint's section 13.3.1.
- **How do I test my provider?** See the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) under "Testing Your Provider".
- **Can I update my provider after it is listed?** Yes. Community providers are versioned independently. If externally hosted, update your NuGet package and open a PR to update the registry entry. If hub-hosted, open a PR to the `community/` folder with your changes.
- **What if I disagree with a maintainer's decision?** Open an issue on this repository or the main vouchfx repository. The platform team is committed to fair, defensible decisions based on the published rubric.

## Licence

All contributions are made under the Apache-2.0 licence and must be compatible with it. By submitting a pull request, you agree to licence your contribution under the Apache-2.0 licence.

---

Thank you for contributing to vouchfx. The community tier model exists because the platform grows through the efforts of contributors like you.
