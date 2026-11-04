# vouchfx-providers

[![Conformance](https://github.com/tomas-rampas/vouchfx-providers/actions/workflows/conformance.yml/badge.svg?branch=main)](https://github.com/tomas-rampas/vouchfx-providers/actions/workflows/conformance.yml?query=branch%3Amain)
[![Vouchfx.Community.JsonRpc](https://img.shields.io/nuget/vpre/Vouchfx.Community.JsonRpc?label=Vouchfx.Community.JsonRpc)](https://www.nuget.org/packages/Vouchfx.Community.JsonRpc)
[![Community providers](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Ftomas-rampas%2Fvouchfx-providers%2Fmain%2Fregistry%2Fcommunity-providers.json&query=%24.length&label=community%20providers&color=blue)](https://github.com/tomas-rampas/vouchfx-providers/blob/main/registry/community-providers.json)
[![Docs](https://img.shields.io/badge/docs-GitHub_Pages-blue)](https://providers.vouchfx.io/)
[![License](https://img.shields.io/github/license/tomas-rampas/vouchfx-providers)](https://github.com/tomas-rampas/vouchfx-providers/blob/main/LICENSE)

The community provider hub for [vouchfx](https://github.com/tomas-rampas/vouchfx) — the curated **community provider index** and the PR-gated hub hosting for Apache-2.0 licensed providers.

> **Documentation site:** [providers.vouchfx.io](https://providers.vouchfx.io/) — rendered from this repository on every push, with the seven-stage [provider authoring journey](https://providers.vouchfx.io/docs/implementing-a-provider.html) as its centrepiece.

A vouchfx *provider* is a `<family>.<provider>` step type (e.g. `db-assert.postgres`, `mq-publish.kafka`) that the vouchfx engine discovers and executes. This repository serves two purposes:

1. **Community Provider Index** — a registry of providers authored by the community, vetted only for Apache-2.0 compliance and the reflective-discovery contract.
2. **Hosted Community Providers** — a PR-gated hub where community members can host their providers; the Vouched badge offers optional post-listing platform-team endorsement.

## .NET compatibility

Current source supports .NET 8, 9 and 10 SDKs. Provider packages retain their
`net8.0` asset baseline so applications on all three versions can reference them.
The [compatibility matrix](https://github.com/tomas-rampas/vouchfx-providers/blob/main/.github/workflows/dotnet-compatibility.yml) builds and
runs template and community conformance tests with each SDK/runtime installed in
isolation. This runtime matrix is separate from the Vouched engine-version rubric.

## What is a Provider?

Providers implement test steps; they are distinct from the mechanism that starts an environment.
Published vouchfx `1.0.0` supports inline service images/projects and managed dependencies, plus
owned, fresh Docker Compose environments with HTTP/TCP services and PostgreSQL dependencies.
The format-neutral environment-driver contract permits other registered drivers; those formats
require their own driver implementation. Starting an environment does not require a step provider.
Protocol assertions use compatible Core or custom step providers. See
[Owned Docker Compose environments](https://vouchfx.io/compose-environments/) for configuration,
readiness and ownership limits.

A provider is a compile-time, source-level plugin to the vouchfx engine. It exposes a new **step type** (a capability, like asserting a database state or publishing a message) by implementing four required interfaces from the `Vouchfx.Sdk`:

- **`IStepProvider`** — your provider's identity and metadata
- **`IStepBinder<TModel>`** — deserialise a YAML step into a strongly-typed model record
- **`IStepValidator<TModel>`** — validate the model with author-friendly diagnostics
- **`IStepCompiler<TModel>`** — emit the C# code (a `CsxFragment`) that runs inside the compiled delegate

For the seven-stage provider authoring journey — from template scaffolding through project setup, the contract surfaces, CSX composition, testing, and publishing — see [`docs/implementing-a-provider.md`](docs/implementing-a-provider.md) (also [rendered on the documentation site](https://providers.vouchfx.io/docs/implementing-a-provider.html)). The engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) remains the authoritative statement of the frozen v1 SDK contract itself.

## The Two Governance Tiers

All vouchfx providers are licensed under Apache-2.0 and are governed in two tiers, plus an optional endorsement badge:

### Core
The providers shipped by the vouchfx team as part of the engine release — currently twenty-five across eleven families (`http` for REST/SOAP, `db-assert` for Postgres/MySQL/SQL Server/MongoDB/DynamoDB, `mq-publish`/`mq-expect` for Kafka/RabbitMQ/NATS/Azure Service Bus/Redis Streams, `cache-assert` for Redis/Elasticsearch, `storage-assert` for S3, `metrics-assert` for Prometheus expositions, `trace-expect` for OTLP traces, `mail-expect`, `webhook-listen`, `script`). The authoritative, always-current list lives in the [engine repository's README](https://github.com/tomas-rampas/vouchfx#providers).

Core providers are bundled with the engine, versioned together, fully supported by the platform team, and reference implementations of the provider contract. Real-world usage of the Core set is showcased on the [vouchfx-samples site](https://samples.vouchfx.io/).

### Community
All community-authored providers with no platform-team endorsement. Community providers:
- May be authored and versioned independently
- Are discoverable through this repository's registry
- Must be Apache-2.0 licensed and follow the reflective-discovery contract
- Can be hosted **externally** (your own repository, published on NuGet) or **hub-hosted** (contributed as source into this repository's `community/` directory — no NuGet account needed; CI runs each provider's tests in isolation and you keep ownership of your folder)

Hosting here is **not** endorsement. The hub's first entry, [`rpc.json-rpc`](community/Vouchfx.Community.JsonRpc/README.md) (`community/Vouchfx.Community.JsonRpc`), is hub-hosted and doubles as the canonical, CI-tested reference implementation used throughout the [provider authoring journey](docs/implementing-a-provider.md).

### The Vouched Badge
After a Community provider is listed in the registry, a maintainer can award the optional **Vouched badge** — registry metadata (`"vouched": true`) signifying platform-team review and endorsement. To earn it, a provider must meet the published rubric in [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md) and undergo security review. The badge does not move the provider to a new tier; it remains Community-hosted and community-owned; it is a post-listing endorsement only. Badge revocation is reversible by a maintainer PR; the provider source stays where it is.

## How to Use Community Providers Today

Community providers are distributed as **NuGet packages** — hub-hosted providers publish from this hub's CI, externally hosted providers publish from their own repositories. For example, [`Vouchfx.Community.JsonRpc` 1.0.0](https://www.nuget.org/packages/Vouchfx.Community.JsonRpc/1.0.0) is published on NuGet.org — the canonical worked example of consuming a community provider is the [`ledger-jsonrpc`](https://samples.vouchfx.io/samples/ledger-jsonrpc/README.html) sample application in vouchfx-samples.

For unpublished or hub-hosted providers without a NuGet package yet, consume via **source-level build**: clone the provider repository (or reference `community/<Provider>` in this hub), add the project to your build, and rebuild your application host to integrate the provider at compile time.

**See [`docs/consuming-a-provider.md`](docs/consuming-a-provider.md)** for the complete guide to both consumption paths, exact version pinning, optional pre-release dependencies, and the worked example.

## How to Get Your Provider Listed

### Community Tier

**Option 1: Open an issue (external hosting)**

If your provider is published on NuGet, click [**New Issue → Provider Listing**](.github/ISSUE_TEMPLATE/provider-listing.yml) and fill in the form. A maintainer will review and add it to the registry.

**Option 2: Submit an entry PR (external hosting)**

Fork this repository, add an entry to `registry/community-providers.json` (following the schema in `registry/community-providers.schema.json`), and open a pull request. See [`registry/README.md`](registry/README.md) for the field meanings.

**Option 3: Submit your provider as source (hub hosting)**

Open a PR adding `community/<YourProvider>/` (+ its `.Tests` project) and a registry entry with `"hosting": "hub"` — no NuGet package required. Use the [community submission template](.github/PULL_REQUEST_TEMPLATE/community-submission.md); the merge bar is licence/DCO/namespace hygiene and a green conformance lane, not a code review. See [CONTRIBUTING.md](CONTRIBUTING.md) for the step-by-step.

### Earning the Vouched Badge

If your Community provider meets the [Vouched rubric](VOUCHED_CHECKLIST.md), you can work towards platform-team endorsement:

1. **Your provider is already listed** — the provider is in the registry and CI is passing.

2. **Open a Vouched request** — open an issue [**New Issue → Vouched Request**](.github/ISSUE_TEMPLATE/vouched-request.yml) linking to your provider source and confirming which rubric items are met. This proposes the provider for review.

3. **Maintainer review** — a maintainer reviews your provider against the published rubric:
   - Integration-test fixture passes on the engine main branch + two preceding minors
   - README with ≥3 worked examples + known-limitations section
   - Security sign-off (credentials, transitive CVEs, TLS, no telemetry, package signature)
   - Apache-2.0 + DCO sign-off
   - `MinEngineVersion` declared in provider metadata
   - CSX reviewed for §13.3.1 conformance

4. **Badge award** — upon approval, a maintainer opens a one-line PR adding `"vouched": true` to your registry entry. Once merged, the badge is live on your registry listing. Revocation is reversible.

## How the Hub Conformance Gate Works

**For hub-hosted source submissions (Option 3)**, CI automatically runs:

1. **Build and format** — the template and solution projects against the SDK pinned in `Directory.Build.props`.
2. **Conformance and community tests** — template tests and each community test project, including its own restore/build.
3. **Package validation** — every hub provider packs with the required licence, README and metadata.
4. **Registry validation** — entries conform to the registry schema; badge changes require maintainer review.

**For external listing submissions (Option 1 or 2)**, CI validates:

- **Registry schema validation** — the entry must match `registry/community-providers.schema.json`. This does not run an externally hosted provider's test suite.

**Note:** For hub-hosted submissions, the required CI lane runs your conformance tests against the pinned SDK (`$(VouchfxSdkVersion)`); public consumers and that lane require the SDK closure to be available on NuGet.org. Private candidate-feed validation does not establish public restore availability. A separate non-blocking lane also builds against the engine's `main` branch as an early warning. The Vouched rubric additionally requires your integration-test fixture to pass on the engine main branch plus the two preceding minor releases; that multi-version validation is a human-review requirement verified by maintainers during the Vouched review, not an automated CI check.

## Building against the SDK

The published `Vouchfx.Sdk` and `Vouchfx.Sdk.Testing` `1.2.0` packages and their
dependency closure are available on NuGet.org. `Directory.Build.props` selects
this SDK version through `$(VouchfxSdkVersion)`; `nuget.config` routes its package
IDs to NuGet.org. The Release build, template tests (5/5), JsonRpc tests (24/24),
and formatting check passed against SDK 1.2.0. The stock Core 1.2.0 CLI also loaded
the current Hello and JsonRpc source providers from a `--providers` directory and ran
them successfully (2/2 steps); a copied Hello template consumer passed 5/5 tests.
From the repository root:

```bash
dotnet restore vouchfx-providers.sln
dotnet build vouchfx-providers.sln -c Release --no-restore -warnaserror
dotnet test template/Vouchfx.Community.Hello.Tests -c Release --no-build --no-restore
dotnet test community/Vouchfx.Community.JsonRpc.Tests/Vouchfx.Community.JsonRpc.Tests.csproj -c Release --no-build --no-restore
dotnet format vouchfx-providers.sln --verify-no-changes --no-restore
```

The [dated Core 1.0.7 custom-host ABI record](docs/validation/core-1.0.7-sdk-1.0.5-compatibility-2026-10-17.md)
documents unchanged SDK 1.0.5 provider assemblies running in a custom host against Core 1.0.7.
It does not validate stock-CLI loading of external providers.

For an unpublished SDK candidate, validation requires its supplied package feed and
restore configuration. Follow [Building against the SDK](CONTRIBUTING.md#building-against-the-sdk),
or use the engine-main override below for unreleased source work.

### Building against engine main (optional)

For advanced contributors who want to test against the engine's unreleased `main` branch, you can build locally against an unpublished SDK by packing the five SDK-closure projects from the engine and overriding the pinned version:

```bash
# From the vouchfx-providers repo root; adjust the engine checkout path:
engine_dir="../vouchfx"
sdk_version="1.0.0-enginemain.sha$(git -C "$engine_dir" rev-parse --short=12 HEAD)"
for p in \
  src/Engine/Vouchfx.Engine.Abstractions/Vouchfx.Engine.Abstractions.csproj \
  src/Engine/Vouchfx.Engine.Authoring/Vouchfx.Engine.Authoring.csproj \
  src/Engine/Vouchfx.Engine.Compilation/Vouchfx.Engine.Compilation.csproj \
  src/Sdk/Vouchfx.Sdk/Vouchfx.Sdk.csproj \
  src/Sdk/Vouchfx.Sdk.Testing/Vouchfx.Sdk.Testing.csproj ; do
  dotnet pack "$engine_dir/$p" -c Release -o packages-local -p:Version="$sdk_version"
done

# Override the pinned version to use the locally packed pre-release
dotnet restore -p:VouchfxSdkVersion="$sdk_version"
dotnet build --no-restore -p:VouchfxSdkVersion="$sdk_version"
```

CI's early-warning lane (Lane B) resolves engine `main` once per run, logs its full SHA and checks out that immutable commit. Its SDK packages use a SHA-specific version, keeping compatibility evidence distinct from the published SDK pin and other source builds.

## Triage and Support

The vouchfx maintainers allocate **one half-day per week** (4 hours) for provider community support: reviewing submissions, triaging issues, and helping new authors. This is a ringfenced, sustainable rate; for urgent issues outside that window, open a GitHub issue and tag `@tomas-rampas` or the vouchfx team.

## Key Documents

- **[`docs/implementing-a-provider.md`](docs/implementing-a-provider.md)** — the provider authoring journey overview and links to each stage (project setup, contract, CSX composition, testing, publishing)
- **[`docs/consuming-a-provider.md`](docs/consuming-a-provider.md)** — how to add published community providers to your test application (NuGet path, source builds, exact pinning and optional pre-release dependencies)
- **[`CONTRIBUTING.md`](CONTRIBUTING.md)** — how to submit a provider to this repository (Community paths and Vouched review)
- **[`GOVERNANCE.md`](GOVERNANCE.md)** — the tier model, Vouched badge, and who decides each
- **[`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md)** — the published Vouched rubric as an actionable checklist
- **[`CHANGELOG.md`](CHANGELOG.md)** — hub release history and delivered capabilities
- **[`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)** — our community standards (Contributor Covenant 2.1)
- **[`registry/community-providers.json`](registry/community-providers.json)** — the curated index of Community-tier providers
- **Engine [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md)** — complete guide to writing a provider (contract, examples, testing, hard rules)
- **Engine [`docs/01`](https://github.com/tomas-rampas/vouchfx/blob/main/docs/01_Technical_Architecture_and_Engineering_Blueprint.md)** § 13 — provider architecture, CsxFragment composition rules (§13.3.1), reserved namespaces (§5.6)

## Directory Layout

```
.
|-- README.md                           (this file)
|-- CONTRIBUTING.md                     (submission flows and Vouched badge)
|-- GOVERNANCE.md                       (tier model and Vouched badge)
|-- VOUCHED_CHECKLIST.md                (Vouched rubric)
|-- CODE_OF_CONDUCT.md                  (community standards)
|-- registry/
|   |-- README.md                       (how to add entries)
|   |-- community-providers.json        (the index)
|   `-- community-providers.schema.json (JSON Schema)
|-- template/                           (starter provider scaffold)
|-- community/                          (hub-hosted Community-tier providers, hub-CI-tested)
|   `-- Vouchfx.Community.JsonRpc/      (rpc.json-rpc — the first community provider; see its README)
|-- packages-local/                     (local SDK feed for optional engine-main builds; used by CI Lane B)
`-- .github/
    |-- ISSUE_TEMPLATE/
    |   |-- provider-listing.yml        (Community listing form)
    |   `-- vouched-request.yml         (Vouched badge request form)
    `-- PULL_REQUEST_TEMPLATE/
        `-- community-submission.md     (Community hub-hosted submission checklist)
```

## Licence

All contributions are made under the Apache-2.0 licence and must be compatible with it. See the root [`LICENSE`](LICENSE) file.

## Questions or Feedback?

- **Writing a provider?** Start with the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) and worked examples ([`Example.Steps.Echo`](https://github.com/tomas-rampas/vouchfx/tree/main/examples/Example.Steps.Echo), [`Example.Steps.Hello`](https://github.com/tomas-rampas/vouchfx/tree/main/examples/Example.Steps.Hello)).
- **Seeking the Vouched badge?** See the [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md) rubric and the Vouched request issue template.
- **Question about vouchfx itself?** Open an issue on the [main repository](https://github.com/tomas-rampas/vouchfx).
- **Community or governance question?** Open an issue here.

---

*Status: This is the active submission hub for vouchfx providers. Contributions welcome.*
