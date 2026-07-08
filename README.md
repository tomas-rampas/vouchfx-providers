# vouchfx-providers

The community provider hub for [vouchfx](https://github.com/tomas-rampas/vouchfx) — the curated **community provider index** and the PR-gated **Verified-tier submission gate** for Apache-2.0 licensed providers.

> **Documentation site:** [tomas-rampas.github.io/vouchfx-providers](https://tomas-rampas.github.io/vouchfx-providers/) — rendered from this repository on every push, with the comprehensive [implementing-a-provider guide](https://tomas-rampas.github.io/vouchfx-providers/docs/implementing-a-provider.html) as its centrepiece.

A vouchfx *provider* is a `<family>.<provider>` step type (e.g. `db-assert.postgres`, `mq-publish.kafka`) that the vouchfx engine discovers and executes. This repository serves two purposes:

1. **Community Provider Index** — a registry of providers authored by the community, vetted only for Apache-2.0 compliance and the reflective-discovery contract.
2. **Verified-Tier Gate** — a PR-gated submission process for providers seeking platform-team endorsement and promotion to the Verified tier.

## What is a Provider?

A provider is a compile-time, source-level plugin to the vouchfx engine. It exposes a new **step type** (a capability, like asserting a database state or publishing a message) by implementing four required interfaces from the `Platform.Sdk`:

- **`IStepProvider`** — your provider's identity and metadata
- **`IStepBinder<TModel>`** — deserialise a YAML step into a strongly-typed model record
- **`IStepValidator<TModel>`** — validate the model with author-friendly diagnostics
- **`IStepCompiler<TModel>`** — emit the C# code (a `CsxFragment`) that runs inside the compiled delegate

For the comprehensive, code-grounded guide to writing a provider — the contract surfaces, the CSX composition rules, verdicts, secrets, capture, testing and the submission checklist — see [`docs/implementing-a-provider.md`](docs/implementing-a-provider.md) (also [rendered on the documentation site](https://tomas-rampas.github.io/vouchfx-providers/docs/implementing-a-provider.html)). The engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) remains the authoritative statement of the frozen v1 SDK contract itself.

## The Three Governance Tiers

All vouchfx providers are licensed under Apache-2.0 and are governed in three tiers:

### Core
The providers shipped by the vouchfx team as part of the engine release — currently twenty-five across eleven families (`http` for REST/SOAP, `db-assert` for Postgres/MySQL/SQL Server/MongoDB/DynamoDB, `mq-publish`/`mq-expect` for Kafka/RabbitMQ/NATS/Azure Service Bus/Redis Streams, `cache-assert` for Redis/Elasticsearch, `storage-assert` for S3, `metrics-assert` for Prometheus expositions, `trace-expect` for OTLP traces, `mail-expect`, `webhook-listen`, `script`). The authoritative, always-current list lives in the [engine repository's README](https://github.com/tomas-rampas/vouchfx#providers).

Core providers are bundled with the engine, versioned together, fully supported by the platform team, and reference implementations of the provider contract. Real-world usage of the Core set is showcased in the [vouchfx-samples](https://github.com/tomas-rampas/vouchfx-samples) repository.

### Verified
Community providers that have passed a published rubric and are endorsed by the platform team. Verified providers:
- Pass their integration-test fixture on the official matrix (engine main + two preceding minor versions)
- Ship a README with at least three realistic use cases and a known-limitations section
- Undergo security sign-off (credentials, dependency vulnerabilities, TLS, telemetry)
- Are Apache-2.0 licensed with DCO sign-off
- Declare MinEngineVersion compatibility
- Have their emitted CSX reviewed for CsxFragment composition correctness (§13.3.1 of the architecture blueprint)

Verified providers are listed on the [project website](https://tomas-rampas.github.io/vouchfx/) and discoverable via NuGet, but **not** bundled with the engine.

## How to Use Community and Verified Providers Today

A non-Core provider (Community or Verified) is consumed via **source-level build**: clone the provider repository, reference its project in your build, and rebuild your application host to integrate the provider at compile time. This is the distribution model through v1.0 and beyond.

A one-command install experience — `vouchfx providers install <community-provider-package>` — is planned and tracked on the [engine's public roadmap](https://tomas-rampas.github.io/vouchfx/docs/roadmap.html). When this provider directory loader ships, packages will become runtime-loadable without requiring source-level builds.

### Community
All other providers, with no platform-team endorsement. Community providers:
- May be authored and versioned independently
- Are discoverable through this repository's registry
- Must be Apache-2.0 licensed and follow the reflective-discovery contract
- Can graduate to Verified by meeting the published rubric

The tier's first entry is [`rpc.json-rpc`](community/Community.Steps.JsonRpc/README.md) (`community/Community.Steps.JsonRpc`), hosted in this repository so the tier has a canonical, CI-tested reference implementation — it is the provider the [implementing-a-provider guide](docs/implementing-a-provider.md) walks through. Community providers normally live in their authors' own repositories; hub hosting is the exception, not the rule.

## How to Get Your Provider Listed

### Community Tier (Index Listing)

If your provider is authored, tested, documented, and published on NuGet, open an issue or submit a PR to list it in the community provider index:

**Option 1: Open an issue**

Click [**New Issue → Provider Listing**](.github/ISSUE_TEMPLATE/provider-listing.yml) and fill in the form. A maintainer will review and add it to the registry.

**Option 2: Submit a PR**

Fork this repository, add an entry to `registry/community-providers.json` (following the schema in `registry/community-providers.schema.json`), and open a pull request. See [`registry/README.md`](registry/README.md) for the field meanings.

### Verified Tier (Submission Gate)

If your provider meets the [Verified-tier rubric](VERIFIED_TIER_CHECKLIST.md), you can submit it for platform-team endorsement:

1. **Propose Intent** — open an issue [**New Issue → Verified Proposal**](.github/ISSUE_TEMPLATE/verified-proposal.yml) linking to your provider repository and confirming which rubric items are met. This signals your intent and lets the maintainers coordinate review timing.

2. **Submit for Review** — open a pull request to this repository:
   - Create a folder at `verified/<your-provider-id>/` (e.g. `verified/snowflake-assert/`)
   - Copy your provider's source code, tests, and README into that folder
   - Include your integration-test fixture (the conformance test that CI will run)
   - Complete the PR checklist in the pull-request template (mirrors the Verified rubric)
   - Ensure `dotnet test verified/<your-provider-id>/` passes locally

3. **CI Conformance Gate** — the CI pipeline runs your provider's conformance tests against the engine `main`/pinned SDK. The two preceding minor versions are validated by a maintainer at review time.

4. **Security Review** — a maintainer conducts security sign-off per the rubric.

5. **CSX Review** — a maintainer reads the emitted C# code for your representative steps and confirms it follows the CsxFragment composition rules.

6. **Merge and Promote** — upon approval, your PR merges and your provider is promoted to Verified. It is listed on the [project website](https://tomas-rampas.github.io/vouchfx/) and added to the public registry.

## How the Conformance Gate Works

When you submit a provider to the `verified/` folder, CI automatically runs:

1. **Compile** — your provider and its test assembly against the `Platform.Sdk`
2. **Unit Tests** — your test suite (unit and integration-capable tests)
3. **Integration Tests** — your integration-test fixture against the engine `main` branch
4. **Schema Validation** — your provider's JSON Schema fragment against the engine's validator

**Note:** CI runs your conformance test against the engine `main` SDK only. The Verified-tier rubric requires you to verify your provider's integration-test fixture also passes on the engine main branch plus the two preceding minor releases; that multi-version validation is a human-review requirement verified by maintainers at submission time, not an automated CI check.

## Building Before v1.0 GA

The vouchfx SDK (`Platform.Sdk` and `Platform.Sdk.Testing`) will be released to [NuGet.org](https://www.nuget.org) with the vouchfx v1.0 **GA** release. The v1.0.0-alpha pre-releases include only the vouchfx CLI tool; the SDK packages are not yet on NuGet. Until the GA release, to build providers locally, pack the five SDK-closure projects from the engine:

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

This repository's committed `nuget.config` already references the `packages-local` source, so local builds will consume the packed SDK.

## Triage and Support

The vouchfx maintainers allocate **one half-day per week** (4 hours) for provider community support: reviewing submissions, triaging issues, and helping new authors. This is a ringfenced, sustainable rate; for urgent issues outside that window, open a GitHub issue and tag `@tomas-rampas` or the vouchfx team.

## Key Documents

- **[`CONTRIBUTING.md`](CONTRIBUTING.md)** — how to submit a provider to this repository (Community or Verified flows)
- **[`GOVERNANCE.md`](GOVERNANCE.md)** — the tier model, promotion path, and who decides each tier
- **[`VERIFIED_TIER_CHECKLIST.md`](VERIFIED_TIER_CHECKLIST.md)** — the published Verified-tier rubric as an actionable checklist
- **[`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)** — our community standards (Contributor Covenant 2.1)
- **[`registry/community-providers.json`](registry/community-providers.json)** — the curated index of Community-tier providers
- **Engine [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md)** — complete guide to writing a provider (contract, examples, testing, hard rules)
- **Engine [`docs/01`](https://github.com/tomas-rampas/vouchfx/blob/main/docs/01_Technical_Architecture_and_Engineering_Blueprint.md)** § 13 — provider architecture, CsxFragment composition rules (§13.3.1), reserved namespaces (§5.6)

## Directory Layout

```
.
├── README.md                           (this file)
├── CONTRIBUTING.md                     (submission flows)
├── GOVERNANCE.md                       (tier model)
├── VERIFIED_TIER_CHECKLIST.md         (Verified rubric)
├── CODE_OF_CONDUCT.md                 (community standards)
├── registry/
│   ├── README.md                       (how to add entries)
│   ├── community-providers.json        (the index)
│   └── community-providers.schema.json (JSON Schema)
├── template/                           (starter provider scaffold)
├── community/                          (hub-hosted Community-tier providers, hub-CI-tested)
│   └── Community.Steps.JsonRpc/        (rpc.json-rpc — the first community provider; see its README)
├── verified/                           (Verified-tier submissions)
│   └── <provider-id>/                  (one folder per submission)
│       ├── src/                        (provider source code)
│       ├── tests/                      (conformance tests)
│       └── README.md                   (provider documentation)
├── packages-local/                     (local SDK feed during pre-v1.0)
└── .github/
    ├── ISSUE_TEMPLATE/
    │   ├── provider-listing.yml        (Community listing form)
    │   └── verified-proposal.yml       (Verified intent-to-submit form)
    └── PULL_REQUEST_TEMPLATE/
        └── verified-submission.md      (Verified submission checklist)
```

## Licence

All contributions are made under the Apache-2.0 licence and must be compatible with it. See the root [`LICENSE`](LICENSE) file.

## Questions or Feedback?

- **Writing a provider?** Start with the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) and worked examples ([`Example.Steps.Echo`](https://github.com/tomas-rampas/vouchfx/tree/main/examples/Example.Steps.Echo), [`Example.Steps.Hello`](https://github.com/tomas-rampas/vouchfx/tree/main/examples/Example.Steps.Hello)).
- **Submitting to Verified?** See the [`VERIFIED_TIER_CHECKLIST.md`](VERIFIED_TIER_CHECKLIST.md) and the Verified submission pull-request template.
- **Question about vouchfx itself?** Open an issue on the [main repository](https://github.com/tomas-rampas/vouchfx).
- **Community or governance question?** Open an issue here.

---

*Status: This is the active submission hub for vouchfx providers. Contributions welcome.*
