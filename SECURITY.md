# Security Policy

This repository is the **community provider hub** for
[vouchfx](https://github.com/tomas-rampas/vouchfx): it hosts the community provider
**registry**, the provider **template scaffold**, and the **Vouched-badge award
process** (the published rubric, the maintainer security/CSX review, and the registry
metadata flag). Because the Vouched badge represents platform-team endorsement — a
trust statement that consumers rely on when choosing providers — we take security
reports about this repository's processes and listings seriously and aim to respond
quickly and transparently.

This document describes how to report a vulnerability, what is in and out of scope,
and our coordinated-disclosure expectations.

## Reporting a vulnerability

**Please do not open a public GitHub issue, pull request, or discussion for a
suspected security vulnerability.** Public disclosure before a fix is available
puts every user at risk.

Report privately via **GitHub private vulnerability reporting on this repository**:
go to **Security → Advisories → Report a vulnerability**
(`https://github.com/tomas-rampas/vouchfx-providers/security/advisories/new`).
This opens a private channel visible only to you and the maintainers, with a
built-in workflow for coordinating the fix and publishing a CVE where warranted.

If you cannot use that route, open a *minimal*, non-revealing public issue that says
only "I have a security report, please provide a private contact" — never include
details, reproduction steps, or proof-of-concept in the public channel.

### What to include

A good report lets us reproduce and triage fast. Please include, where possible:

- A clear description of the issue and the **impact** (what an attacker can do).
- The **affected surface**: the registry, the template scaffold, a CI workflow, the
  Vouched-badge rubric or review process, or a specific listed provider.
- **Reproduction steps** — ideally a minimal submission, registry entry, or workflow
  input that demonstrates the bypass.
- Any **proof-of-concept**, logs, or workflow runs (redact your own secrets first).
- Your assessment of **severity** and any known mitigations or workarounds.

## Our commitment (coordinated disclosure)

When you report privately, we will:

| Stage | Target |
| --- | --- |
| **Acknowledge** receipt of your report | within **3 business days** |
| **Initial assessment** (validity, severity, scope) | within **7 business days** |
| **Status updates** while we work a confirmed issue | at least every **7 business days** |
| **Fix or mitigation** for confirmed High/Critical issues | targeted within **90 days** of confirmation |

We follow a **coordinated-disclosure** model:

- We will work with you on a disclosure timeline and a mutually agreed publication
  date. Our default embargo is up to **90 days**, sooner if a fix ships earlier, and
  we may extend it for complex fixes — always in communication with you.
- We will publish a **GitHub Security Advisory** (and request a CVE where warranted)
  when the fix is released.
- We will **credit** you in the advisory and release notes unless you ask to remain
  anonymous. We do not currently operate a paid bug-bounty programme.
- We ask that you give us reasonable time to remediate before any public disclosure,
  and that your testing does not harm users, degrade services, or access data that
  is not yours.

## Scope

### In scope

Security issues in the parts of this repository the project maintains:

- **The community registry** (`registry/community-providers.json`, its schema, and
  the validation that gates entries) — for example, a registry entry crafted to
  mislead consumers about a package's identity, or to inject content into tooling
  that renders the registry.
- **The template scaffold** — a defect in the published provider template that
  would cause providers built from it to be insecure by default.
- **The Vouched-badge award process** — the CI conformance workflows, the published
  rubric (VOUCHED_CHECKLIST.md), and the review process. **Process and rubric
  bypasses belong here**: any way to obtain a Vouched badge without genuinely passing
  the conformance tests, the security sign-off, or the CSX review; workflow injection
  via a submission pull request; or secret/token exfiltration from the conformance
  CI.
- **Malicious or compromised listings**: if a provider listed in this repository
  (any tier) is actively malicious or its package has been compromised, report it
  here as well as upstream — delisting and consumer notification are this
  repository's responsibility.

### Out of scope

- **Provider-specific vulnerabilities in Community-tier providers.** Community
  providers are authored, published, and maintained by their respective authors;
  report a vulnerability in a provider's own code **upstream with the provider
  author**, via that project's security process. We will help coordinate if a
  shared SDK issue is implicated, and (as above) delisting a demonstrably malicious
  or compromised listing is in scope here.
- **The vouchfx engine, Core providers, and Provider SDK** — report those via the
  engine repository's
  [SECURITY.md](https://github.com/tomas-rampas/vouchfx/blob/main/SECURITY.md).
- **Vulnerabilities in upstream dependencies** themselves (GitHub Actions, the .NET
  SDK, NuGet, etc.) — report those to the upstream project. If this repository's
  *use* of a dependency is what creates the exposure, that is in scope.
- Issues requiring a **malicious maintainer**, physical access to a developer
  machine, or an already-compromised CI runner outside this repository's control.
- Reports generated solely by automated scanners with no demonstrated impact,
  best-practice/"hardening" suggestions with no concrete exploit, and social
  engineering of maintainers.

## Governance note

The Vouched-badge bar — conformance testing, security sign-off (credential
handling, transitive CVEs, TLS defaults, no telemetry, package signature), and CSX
review — is defined in the published rubric (`VOUCHED_CHECKLIST.md`) and
[`CONTRIBUTING.md`](CONTRIBUTING.md). It exists precisely because a Vouched listing
is a trust statement; weaknesses in that bar are security issues, not process nits.

---

_This policy is a living document, aligned with the vouchfx engine's
[security policy](https://github.com/tomas-rampas/vouchfx/blob/main/SECURITY.md).
The security contact and the disclosure SLAs will be confirmed as the project moves
from adoption stage to a stable v1 release._
