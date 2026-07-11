# vouchfx-providers

The community provider hub for [vouchfx](https://github.com/tomas-rampas/vouchfx) — the curated **community provider index** and the PR-gated hub hosting for Apache-2.0 licensed providers.

> **Documentation site:** [tomas-rampas.github.io/vouchfx-providers](https://tomas-rampas.github.io/vouchfx-providers/) — rendered from this repository on every push, with the comprehensive [implementing-a-provider guide](https://tomas-rampas.github.io/vouchfx-providers/docs/implementing-a-provider.html) as its centrepiece.

A vouchfx *provider* is a `<family>.<provider>` step type (e.g. `db-assert.postgres`, `mq-publish.kafka`) that the vouchfx engine discovers and executes. This repository serves two purposes:

1. **Community Provider Index** — a registry of providers authored by the community, vetted only for Apache-2.0 compliance and the reflective-discovery contract.
2. **Hosted Community Providers** — a PR-gated hub where community members can host their providers; the Vouched badge offers optional post-listing platform-team endorsement.

## What is a Provider?

A provider is a compile-time, source-level plugin to the vouchfx engine. It exposes a new **step type** (a capability, like asserting a database state or publishing a message) by implementing four required interfaces from the `Vouchfx.Sdk`:

- **`IStepProvider`** — your provider's identity and metadata
- **`IStepBinder<TModel>`** — deserialise a YAML step into a strongly-typed model record
- **`IStepValidator<TModel>`** — validate the model with author-friendly diagnostics
- **`IStepCompiler<TModel>`** — emit the C# code (a `CsxFragment`) that runs inside the compiled delegate

For the comprehensive, code-grounded guide to writing a provider — the contract surfaces, the CSX composition rules, verdicts, secrets, capture, testing and the submission checklist — see [`docs/implementing-a-provider.md`](docs/implementing-a-provider.md) (also [rendered on the documentation site](https://tomas-rampas.github.io/vouchfx-providers/docs/implementing-a-provider.html)). The engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) remains the authoritative statement of the frozen v1 SDK contract itself.

## The Two Governance Tiers

All vouchfx providers are licensed under Apache-2.0 and are governed in two tiers, plus an optional endorsement badge:

### Core
The providers shipped by the vouchfx team as part of the engine release — currently twenty-five across eleven families (`http` for REST/SOAP, `db-assert` for Postgres/MySQL/SQL Server/MongoDB/DynamoDB, `mq-publish`/`mq-expect` for Kafka/RabbitMQ/NATS/Azure Service Bus/Redis Streams, `cache-assert` for Redis/Elasticsearch, `storage-assert` for S3, `metrics-assert` for Prometheus expositions, `trace-expect` for OTLP traces, `mail-expect`, `webhook-listen`, `script`). The authoritative, always-current list lives in the [engine repository's README](https://github.com/tomas-rampas/vouchfx#providers).

Core providers are bundled with the engine, versioned together, fully supported by the platform team, and reference implementations of the provider contract. Real-world usage of the Core set is showcased in the [vouchfx-samples](https://github.com/tomas-rampas/vouchfx-samples) repository.

### Community
All community-authored providers with no platform-team endorsement. Community providers:
- May be authored and versioned independently
- Are discoverable through this repository's registry
- Must be Apache-2.0 licensed and follow the reflective-discovery contract
- Can be hosted **externally** (your own repository, published on NuGet) or **hub-hosted** (contributed as source into this repository's `community/` directory — no NuGet account needed; CI runs each provider's tests in isolation and you keep ownership of your folder)

Hosting here is **not** endorsement. The hub's first entry, [`rpc.json-rpc`](community/Vouchfx.Community.JsonRpc/README.md) (`community/Vouchfx.Community.JsonRpc`), is hub-hosted and doubles as the canonical, CI-tested reference implementation the [implementing-a-provider guide](docs/implementing-a-provider.md) walks through.

### The Vouched Badge
After a Community provider is listed in the registry, a maintainer can award the optional **Vouched badge** — registry metadata (`"vouched": true`) signifying platform-team review and endorsement. To earn it, a provider must meet the published rubric in [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md) and undergo security review. The badge does not move the provider to a new tier; it remains Community-hosted and community-owned; it is a post-listing endorsement only. Badge revocation is reversible by a maintainer PR; the provider source stays where it is.

## How to Use Community Providers Today

Community providers are distributed as **NuGet packages** — hub-hosted providers publish from this hub's CI, externally hosted providers publish from their own repositories. For example, [`Vouchfx.Community.JsonRpc`](https://www.nuget.org/packages/Vouchfx.Community.JsonRpc) is live on NuGet.org — the canonical worked example of consuming a community provider is the [`ledger-jsonrpc`](https://github.com/tomas-rampas/vouchfx-samples/tree/main/samples/ledger-jsonrpc) sample application in vouchfx-samples.

For unpublished or hub-hosted providers without a NuGet package yet, consume via **source-level build**: clone the provider repository (or reference `community/<Provider>` in this hub), add the project to your build, and rebuild your application host to integrate the provider at compile time.

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

1. **Compile** — your provider and its test assembly against the `Vouchfx.Sdk`
2. **Unit Tests** — your test suite (unit and integration-capable tests)
3. **Integration Tests** — your integration-test fixture against the engine `main` branch
4. **Schema Validation** — your provider's JSON Schema fragment against the engine's validator

**For external listing submissions (Option 1 or 2)**, CI validates:

- **Schema Validation only** — your registry entry's JSON Schema fragment

**Note:** For hub-hosted submissions, the required CI lane runs your conformance tests against the published pinned SDK (`$(VouchfxSdkVersion)`) — exactly what consumers restore; a separate non-blocking lane also builds them against the engine's `main` branch as an early warning. The Vouched rubric additionally requires your integration-test fixture to pass on the engine main branch plus the two preceding minor releases; that multi-version validation is a human-review requirement verified by maintainers during the Vouched review, not an automated CI check.

## Building against the SDK

The vouchfx SDK (`Vouchfx.Sdk` and `Vouchfx.Sdk.Testing`) is published to [NuGet.org](https://www.nuget.org) and pinned in `Directory.Build.props` via the `$(VouchfxSdkVersion)` property; it restores from NuGet.org at that version. To build providers locally:

```bash
dotnet restore
dotnet build
```

That's it. The `nuget.config` in this repository is already configured to restore from NuGet.org; `dotnet restore` will pull the SDK at the pinned version. Bumping the SDK (when the engine releases a new version) requires a single property change in `Directory.Build.props` — maintainers do this at engine release cadence.

### Building against engine main (optional)

For advanced contributors who want to test against the engine's unreleased `main` branch, you can build locally against an unpublished SDK by packing the five SDK-closure projects from the engine and overriding the pinned version:

```bash
# From the vouchfx-providers repo root, with the engine checked out at <engine>:
for p in \
  src/Engine/Vouchfx.Engine.Abstractions/Vouchfx.Engine.Abstractions.csproj \
  src/Engine/Vouchfx.Engine.Authoring/Vouchfx.Engine.Authoring.csproj \
  src/Engine/Vouchfx.Engine.Compilation/Vouchfx.Engine.Compilation.csproj \
  src/Sdk/Vouchfx.Sdk/Vouchfx.Sdk.csproj \
  src/Sdk/Vouchfx.Sdk.Testing/Vouchfx.Sdk.Testing.csproj ; do
  dotnet pack "<engine>/$p" -c Release -o packages-local
done

# Override the pinned version to use the locally packed pre-release
dotnet restore -p:VouchfxSdkVersion=1.0.0-enginemain
dotnet build
```

This is the pattern CI's early-warning lane (Lane B) uses to detect compatibility issues with engine `main` before the next engine release is cut.

## Triage and Support

The vouchfx maintainers allocate **one half-day per week** (4 hours) for provider community support: reviewing submissions, triaging issues, and helping new authors. This is a ringfenced, sustainable rate; for urgent issues outside that window, open a GitHub issue and tag `@tomas-rampas` or the vouchfx team.

## Key Documents

- **[`CONTRIBUTING.md`](CONTRIBUTING.md)** — how to submit a provider to this repository (Community paths and Vouched review)
- **[`GOVERNANCE.md`](GOVERNANCE.md)** — the tier model, Vouched badge, and who decides each
- **[`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md)** — the published Vouched rubric as an actionable checklist
- **[`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)** — our community standards (Contributor Covenant 2.1)
- **[`registry/community-providers.json`](registry/community-providers.json)** — the curated index of Community-tier providers
- **Engine [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md)** — complete guide to writing a provider (contract, examples, testing, hard rules)
- **Engine [`docs/01`](https://github.com/tomas-rampas/vouchfx/blob/main/docs/01_Technical_Architecture_and_Engineering_Blueprint.md)** § 13 — provider architecture, CsxFragment composition rules (§13.3.1), reserved namespaces (§5.6)

## Directory Layout

```
.
├── README.md                           (this file)
├── CONTRIBUTING.md                     (submission flows and Vouched badge)
├── GOVERNANCE.md                       (tier model and Vouched badge)
├── VOUCHED_CHECKLIST.md                (Vouched rubric)
├── CODE_OF_CONDUCT.md                 (community standards)
├── registry/
│   ├── README.md                       (how to add entries)
│   ├── community-providers.json        (the index)
│   └── community-providers.schema.json (JSON Schema)
├── template/                           (starter provider scaffold)
├── community/                          (hub-hosted Community-tier providers, hub-CI-tested)
│   └── Vouchfx.Community.JsonRpc/      (rpc.json-rpc — the first community provider; see its README)
├── packages-local/                     (local SDK feed for optional engine-main builds; used by CI Lane B)
└── .github/
    ├── ISSUE_TEMPLATE/
    │   ├── provider-listing.yml        (Community listing form)
    │   └── vouched-request.yml         (Vouched badge request form)
    └── PULL_REQUEST_TEMPLATE/
        └── community-submission.md     (Community hub-hosted submission checklist)
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
