# Governance: The Two Tiers and the Vouched Badge

This document describes the governance model for vouchfx providers: the two tiers, who decides each tier, the Vouched badge award flow, and how providers move between tiers.

## The Two Tiers

All vouchfx providers are licensed under Apache-2.0. They are governed in two tiers that differ in endorsement level, bundling, and support:

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

**Support:** Full support by the platform team. Issues and feature requests are tracked in the main engine repository.

**Promotion to Core:** A provider may be promoted to Core by the platform team when its scope, adoption, and maintenance burden justify bundling with the engine. Core promotion removes the provider from the community registry entirely—Core providers are engine-repository citizens, not registry entries.

### Tier 2: Community

**Status:** Community-authored, independently versioned, no platform-team endorsement by default (unless Vouched).

Community providers are listed in the curated community index but are not endorsed by the platform team by default. They may be hosted in either of two first-class places: **externally** (the author's own repository, published on NuGet) or **hub-hosted** (contributed as source into this repository's `community/` directory via pull request, no NuGet account required—CI discovers and runs each provider's tests in isolation, and the author retains ownership of their folder via CODEOWNERS).

**Hosting is not endorsement.** The Vouched badge's meaning is *trust*; the Community tier's meaning is *availability*. A provider living under `community/` in this repository carries exactly the same no-endorsement status as one living in its author's repository—every hub-hosted provider's README opens with the Community-tier notice, and the merge bar for accepting one is hygiene, not review.

**Requirements:**
- Apache-2.0 licensed (or compatible); for hub-hosted submissions, all commits signed off via DCO
- Follows the reflective-discovery contract (implements `[StepProvider]` attribute, four required interfaces, uses non-reserved namespaces)
- For hub-hosted submissions: builds standalone against the packed SDK and its conformance lane is green
- No code review or rubric validation by the platform team (that is the Vouched badge gate)

**Decision maker:** Any contributor. Providers are listed by the community via issue or pull request; the gatekeeping is Apache-2.0 compliance, and—for hub-hosted source—DCO, namespace hygiene, and a green conformance lane.

**Delisting (hub-hosted):** A hub-hosted provider whose conformance lane rots and whose author is unresponsive may be delisted—its registry entry removed and its folder archived out of CI—never silently adopted or maintained by the platform team. The source history remains available; the author can resubmit at any time.

**Support:** No official support from the platform team. Provider authors own support for their providers. The published Vouched checklist is the actionable feedback for what is needed to earn the Vouched badge.

---

## The Vouched Badge

**Status:** Maintainer-awarded recognition that a Community provider has passed the published rubric.

The Vouched badge is a registry metadata flag (`"vouched": true`) that signals platform-team endorsement of a Community provider's quality, security, and conformance. A provider may remain in Community tier indefinitely without the badge—there is no implied criticism. The badge is awarded when:

1. A provider is already listed in the registry (Community tier)
2. The provider author (or a user on their behalf) opens a **Vouched request** issue with evidence
3. A maintainer reviews the evidence against the six-item rubric in [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md)
4. If all items pass, the maintainer opens a one-line registry PR setting `"vouched": true` and linking the review issue
5. The PR merges, and the badge appears in the registry

**The six rubric items:**
1. Conformance matrix: integration tests pass on engine main + two preceding minor versions
2. Documentation: README with ≥3 use cases and known-limitations section
3. Security sign-off: credentials, transitive CVEs, TLS defaults, telemetry, package signature
4. Apache-2.0 licence and DCO sign-off
5. MinEngineVersion declared
6. CSX conforms to §13.3.1 of the architecture blueprint

See [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md) for the full rubric and the [vouched-request issue template](.github/ISSUE_TEMPLATE/vouched-request.yml) for the request form.

**Award flow:**

```
[Provider listed in registry]
              ↓
    [Author opens Vouched request issue]
         with rubric evidence
              ↓
    [Maintainer reviews against checklist]
       (conformance, security, CSX, etc.)
              ↓
         [On success]
              ↓
  [Maintainer opens registry PR]
      (one-line: vouched: true)
              ↓
    [PR merges → badge live in registry]
```

**Decision maker:** A vouchfx platform-team maintainer, acting on evidence presented in the Vouched request issue.

**Revocation:** A maintainer may revoke the Vouched badge if a provider violates the rubric (e.g. a high-severity CVE is discovered). The maintainer will notify the author before taking action.

**Badge vs tier:** The Vouched badge and tier are separate concepts. A provider can be Community + Vouched. Tier changes (e.g. Community → Core) require architecture and business decisions by the platform team; the badge recognises quality within a tier.

---

## Who Decides Each Tier and Badge?

| Tier/Badge | Decision Maker | Process | Authority |
|---|---|---|---|
| **Core** | vouchfx platform team | Architectural decision; Core providers are reference implementations and set the quality bar | Highest; bundled with engine |
| **Community** | Any contributor | Open listing; no gatekeeping beyond Apache-2.0 licence and the reflective-discovery contract | Lowest; availability only |
| **Vouched badge** | vouchfx platform team (a maintainer) | Rubric-based gate per [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md); maintainer review; all six items must pass | High; endorsement within Community |

The Vouched-badge decision is objective and rule-based: the rubric is published, the evidence is documented, and the security/CSX reviews are transparent. This design makes the gate fair, defensible, and durable across maintainer turnover.

---

## Governance Principles

### 1. Apache-2.0 Everywhere
All tiers use Apache-2.0 licensing. This ensures providers can move between tiers without IP friction and keeps the ecosystem aligned.

### 2. Publish the Rubric
The Vouched checklist is published as a detailed rubric, not as prose, because checklists drive behaviour. See [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md) for the full details. This transparency is what makes the gate fair and defensible.

### 3. Sustainable Triage Budget
The vouchfx maintainers allocate **one half-day per week** (4 hours) for provider community support: reviewing issues, triaging submissions, and helping new authors. This is a ringfenced, sustainable rate. For urgent issues outside that window, open a GitHub issue.

### 4. DCO Sign-Off, Not CLA
Contributors sign off via the Developer Certificate of Origin (DCO) rather than a Contributor Licence Agreement. The DCO is lighter-weight, requires no separate signing infrastructure, integrates with GitHub, and is consistent with the .NET Foundation's practice. You sign off by committing with `git commit -s` or using the GitHub web UI "Sign off" checkbox.

### 5. Feedback, Not Rejection
If a provider does not meet the Vouched rubric, it remains in Community with no implied criticism. The published rubric is the actionable feedback for what is needed to earn the badge. Authors are welcome to ask for help or guidance.

### 6. No Forced Tier Changes or Badge Revocation Without Notice
Once a provider is listed in Community, it is not moved without the author's consent. A Vouched provider's badge may be revoked only if the provider violates the rubric, and the maintainers will notify the author before taking action.

---

## Tier Transitions and Promotions

### Community → Vouched (Badge Award)

A Community provider earns the Vouched badge by meeting the rubric. This is the most common path—the provider stays in Community tier, but receives platform-team endorsement via the badge.

**Path:**
1. Provider is listed in Community tier
2. Author opens a Vouched request issue (per [template](.github/ISSUE_TEMPLATE/vouched-request.yml)) with evidence of all six rubric items
3. Maintainer reviews the issue and, if all items pass, opens a one-line registry PR setting `"vouched": true`
4. Badge appears in the registry

### Community → Core (Platform Decision)

A Community provider may be promoted to Core by the platform team when:
- It reaches sustained adoption and community reliance
- Its scope and maintenance burden justify bundling
- The platform team commits to long-term support and versioning alignment

**Path:**
- A maintainer proposes promotion to the platform team
- The decision is made via the team's governance process
- On promotion, the provider is removed from the community registry (Core providers are engine-repository citizens only)

### Vouched → Revoked (Policy Violation)

A Vouched provider's badge may be revoked if:
- A high-severity security issue is discovered and not remediated
- The provider violates another rubric item and the author does not respond to requests for fix

**Path:**
1. Maintainer identifies the issue and opens an issue on the provider
2. Maintainer notifies the author of the violation and the revocation timeline
3. If not remediated within a reasonable timeframe, maintainer opens a registry PR removing `"vouched": true`
4. Badge is removed from the registry
5. Provider remains in Community tier

---

## Evolution of the Governance Model

This model is stable for vouchfx v1.x. Post-v1.0, the project may evolve:

- **Maintainer diversity:** Additional maintainers may be added to share the Vouched-badge decision load
- **Subcommittees:** Domain-specific committees (database providers, messaging, etc.) may be formed to deepen expertise in specialist areas
- **Tier refinement:** Additional tiers or criteria may be introduced based on community feedback

Any major changes to governance will be proposed and debated in the community via RFC (Request for Comments) before adoption.

### Evolution Note: Verified Tier Collapsed (2026-07-09)

The former Verified tier — a separate submission lane under `verified/` with its own PR template, issue form and CI step — was collapsed into the Vouched badge. **Rationale:** the tier doubled every governance surface (two directories, two CI globs, two PR templates, two issue forms) whilst attracting no submissions in its lifetime. The Vouched badge carries the same trust signal (platform-team endorsement against the published rubric) as registry metadata awarded after listing, so a provider's source never moves and there is a single contribution flow. The `verified/` directory, its CI step and its submission templates were retired; the rubric survives as [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md).

---

## See Also

- [`VOUCHED_CHECKLIST.md`](VOUCHED_CHECKLIST.md) — the full Vouched-badge rubric as an actionable checklist
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — how to submit a provider and how to request the Vouched badge
- [`README.md`](README.md) — the community provider hub overview
- [`registry/README.md`](registry/README.md) — how the registry works and how to add entries
- Engine [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) — how to write a provider
- Engine [`GOVERNANCE.md`](https://github.com/tomas-rampas/vouchfx/blob/main/GOVERNANCE.md) — the project's overall governance model
- Engine [`docs/roadmap.md`](https://github.com/tomas-rampas/vouchfx/blob/main/docs/roadmap.md) — public roadmap including provider discovery tooling
