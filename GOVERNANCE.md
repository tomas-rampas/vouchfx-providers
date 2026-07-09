# Governance: The Three Tiers and Promotion Path

This document describes the governance model for vouchfx providers, the three tiers, who decides each tier, and how providers graduate across tiers.

## The Three Tiers

All vouchfx providers are licensed under Apache-2.0. They are governed in three tiers that differ in endorsement level, bundling, and support:

### Tier 1: Core
**Status:** Platform team–authored, bundled with the engine, fully supported.

Core providers are shipped as part of the vouchfx engine release and are versioned together with the engine. The vouchfx v1.0 engine includes twenty-five Core providers across eleven families: `http` (REST, SOAP), `db-assert` (Postgres, MySQL, SQL Server, MongoDB, DynamoDB), `mq-publish` and `mq-expect` (Kafka, RabbitMQ, NATS, Azure Service Bus, Redis Streams), `cache-assert` (Redis, Elasticsearch), `storage-assert` (S3), `metrics-assert` (Prometheus), `trace-expect` (OTLP), `mail-expect`, `webhook-listen`, and `script`. For the authoritative, always-current list of Core providers by family, see the [engine repository's README](https://github.com/tomas-rampas/vouchfx#providers).

**Requirements:**
- Authored by the vouchfx platform team
- Apache-2.0 licensed
- Fully documented with use cases and edge cases
- Complete test coverage including Docker integration tests
- CSX conforms to §13.3.1 of the architecture blueprint
- Published in the engine repository and bundled at release

**Decision maker:** The vouchfx platform team (maintainers).

**Support:** Full support by the platform team. Issues and feature requests are tracked in the main repository.

### Tier 2: Verified
**Status:** Community-authored, independently versioned, platform-team–endorsed.

Verified providers have passed a published rubric, are endorsed by the platform team, and are listed on the project website. They are discoverable via NuGet but **not** bundled with the engine.

**Requirements:**
1. Integration-test fixture passes on the official conformance matrix:
   - Engine main branch (`1.0.0` or later)
   - Two preceding minor versions (validated by a maintainer during review)
2. README contains worked examples covering at least three realistic use cases plus a known-limitations section
3. Security sign-off completed:
   - Credential handling reviewed for correctness
   - Transitive dependency vulnerabilities scanned (zero high-severity at promotion)
   - TLS defaults inspected
   - No telemetry phoning home
   - Package signature verified
4. Apache-2.0 license (or compatible) and contributor signs off via DCO (Developer Certificate of Origin)
5. Provider declares a `MinEngineVersion` compatible with the engine's current major version
6. At least one platform-team maintainer has read the emitted CSX for the provider's representative steps and confirmed it follows the CsxFragment composition contract in the architecture blueprint's section 13.3.1

**Decision maker:** A vouchfx platform-team maintainer, after CI and security review pass.

**Support:** Community support. Issues are tracked in this repository. The platform team allocates one half-day per week for provider triage and community support (ringfenced, sustainable rate).

**Path to Verified:** A Community provider meets the rubric above and submits a pull request to this repository with its conformance tests and security sign-off. CI runs the conformance tests against the engine `main`/pinned SDK; maintainers validate the fixture also passes on the engine main branch plus the two preceding minor releases. Upon passing CI, security review, and CSX review, the PR merges and the provider is promoted to Verified.

### Tier 3: Community
**Status:** Community-authored, independently versioned, no platform-team endorsement.

Community providers are listed in the curated community index but are not endorsed by the platform team. They may be hosted in either of two first-class places: **externally** (the author's own repository, published on NuGet) or **hub-hosted** (contributed as source into this repository's `community/` directory via pull request, no NuGet account required — CI discovers and runs each provider's tests in isolation, and the author retains ownership of their folder via CODEOWNERS).

**Hosting is not endorsement.** The Verified tier's meaning is *review*; the Community tier's meaning is *availability*. A provider living under `community/` in this repository carries exactly the same no-endorsement status as one living in its author's repository — every hub-hosted provider's README opens with the Community-tier notice, and the merge bar for accepting one is hygiene, not review.

**Requirements:**
- Apache-2.0 licensed (or compatible); for hub-hosted submissions, all commits signed off via DCO
- Follows the reflective-discovery contract (implements `[StepProvider]` attribute, four required interfaces, uses non-reserved namespaces)
- For hub-hosted submissions: builds standalone against the packed SDK and its conformance lane is green
- No code review or rubric validation by the platform team (that is the Verified gate)

**Decision maker:** Any contributor. Providers are listed by the community via issue or pull request; the gatekeeping is Apache-2.0 compliance, and — for hub-hosted source — DCO, namespace hygiene and a green conformance lane.

**Delisting (hub-hosted):** a hub-hosted provider whose conformance lane rots and whose author is unresponsive may be delisted — its registry entry removed and its folder archived out of CI — never silently adopted or maintained by the platform team. The source history remains available; the author can resubmit at any time.

**Support:** No official support from the platform team. Provider authors own support for their providers. The published Verified-tier rubric is the feedback for what is needed to graduate to Verified.

**Path to Verified:** A Community provider may submit for Verified-tier endorsement at any time by meeting the six rubric items above and opening a PR to this repository.

## Promotion Path

```
[Author writes provider in own repo]
              ↓
         [Publish on NuGet]
              ↓
    ┌─────────────────────────────┐
    │                             │
    v                             v
[Community Tier]         [Signal Verified Intent]
(curated index)                   │
    │                             │
    │      ┌──────────────────────┘
    │      │
    │      ├─ [Implement 6 rubric items]
    │      │  ├─ Integration tests on official matrix
    │      │  ├─ README with use cases
    │      │  ├─ Security sign-off
    │      │  ├─ Apache-2.0 + DCO
    │      │  ├─ MinEngineVersion declared
    │      │  └─ CSX reviewed for §13.3.1
    │      │
    │      ├─ [Submit PR to verified/ folder]
    │      │
    │      ├─ [CI conformance gate runs]
    │      │
    │      ├─ [Maintainer security review]
    │      │
    │      ├─ [Maintainer CSX review]
    │      │
    │      └─ [Merge → Verified Tier]
    │             ↓
    │      [Website listing]
    │      [Public registry entry]
    │      [Platform communications]
    │
    └──────────────────────────────────┘
```

A provider enters the Community tier when it is listed in the registry. It may remain in Community indefinitely — there is no time limit. If the author chooses to pursue Verified endorsement, they submit a PR and the process above begins. If they do not meet all six rubric items, or do not wish to pursue Verified, they remain in Community with no implied criticism.

## Who Decides Each Tier?

| Tier | Decision Maker | Process |
|------|---|---|
| **Core** | vouchfx platform team | Architectural decision; Core providers are reference implementations and are authored by the team |
| **Verified** | vouchfx platform team (a maintainer) | Rubric-based gate: CI passes all conformance tests, security review approves, CSX review confirms §13.3.1 conformance |
| **Community** | Any contributor | Open listing; no gatekeeping beyond Apache-2.0 license and the reflective-discovery contract |

The Verified-tier decision is objective and rule-based: the rubric is published, the conformance gate is automated, and the security/CSX reviews are documented. This design makes the gate fair, defensible, and durable across maintainer turnover.

## Governance Principles

### 1. Apache-2.0 Everywhere
All tiers use Apache-2.0 licensing. This ensures providers can move between tiers without IP friction and keeps the ecosystem aligned.

### 2. Publish the Rubric
The Verified-tier rubric is published as a checklist, not as prose, because checklists drive behaviour. See [`VERIFIED_TIER_CHECKLIST.md`](VERIFIED_TIER_CHECKLIST.md) for the full details. This transparency is what makes the gate fair and defensible.

### 3. Sustainable Triage Budget
The vouchfx maintainers allocate **one half-day per week** (4 hours) for provider community support: reviewing issues, triaging submissions, and helping new authors. This is a ringfenced, sustainable rate. For urgent issues outside that window, open a GitHub issue.

### 4. DCO Sign-Off, Not CLA
Contributors sign off via the Developer Certificate of Origin (DCO) rather than a Contributor Licence Agreement. The DCO is lighter-weight, requires no separate signing infrastructure, integrates with GitHub, and is consistent with the .NET Foundation's practice. You sign off by committing with `git commit -s` or using the GitHub web UI "Sign off" checkbox.

### 5. Feedback, Not Rejection
If a provider does not meet the Verified rubric, it remains in Community with no implied criticism. The published rubric is the actionable feedback for what is needed to graduate. Authors are welcome to ask for help or guidance.

### 6. No Forced Upgrades or Downgrades
Once a provider is listed in a tier, it is not forcibly moved without the author's consent. A Verified provider may be downgraded to Community only if it violates the Verified contract (e.g. a high-severity CVE is discovered), and the maintainers will notify the author before taking action.

## Evolution of the Governance Model

This model is stable for vouchfx v1.x. Post-v1.0, the project may evolve:

- **Maintainer diversity:** additional maintainers may be added to share the Verified-tier decision load
- **Subcommittees:** domain-specific committees (database providers, messaging, etc.) may be formed to deepen expertise in specialist areas
- **Tier refinement:** additional tiers or criteria may be introduced based on community feedback

Any major changes to governance will be proposed and debated in the community via RFC (Request for Comments) before adoption.

## See Also

- [`VERIFIED_TIER_CHECKLIST.md`](VERIFIED_TIER_CHECKLIST.md) — the full Verified-tier rubric as an actionable checklist
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — how to submit a provider (Community or Verified flows)
- [`README.md`](README.md) — the community provider hub overview
- Engine [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) — how to write a provider
- Engine [`GOVERNANCE.md`](https://github.com/tomas-rampas/vouchfx/blob/main/GOVERNANCE.md) — the project's overall governance model
- Engine [`docs/roadmap.md`](https://github.com/tomas-rampas/vouchfx/blob/main/docs/roadmap.md) — public roadmap including the planned provider directory loader
