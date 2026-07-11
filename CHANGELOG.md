# Changelog

All notable changes to the vouchfx provider hub are documented in this file.

The format follows the spirit of [Keep a Changelog](https://keepachangelog.com/). The hub repository
itself is not versioned — entries are dated milestones. The provider packages the hub publishes carry
their own semantic versions via tags of the form `<Provider>/vX.Y.Z` (e.g.
`Vouchfx.Community.JsonRpc/v1.0.0-alpha.1`), and each package's releases are the authoritative record
for that provider.

## [Unreleased]

### Changed

- **Provider authoring guide restructured into a seven-stage journey.** The monolithic
  1,155-line `implementing-a-provider.md` is now an overview and journey map, with the substance in
  focused stage pages: `provider-project-setup.md`, `provider-contract.md`,
  `provider-csx-composition.md`, `provider-testing.md` and `provider-publishing.md`, ending at the
  registry and the Vouched checklist. Every page carries a stage breadcrumb and a "Next" link.

### Added

- **`docs/consuming-a-provider.md`** — the consumer-side guide: the NuGet path (exact pre-release
  pinning and NuGet's NU5104 rule), the source-build path for unpublished providers, the
  `ledger-jsonrpc` sample as the canonical worked example, and the planned
  `vouchfx providers install` experience.
- **This changelog.**

## 2026-07-11

### Changed

- **Publication truth-up (#18)** — consumer-facing docs no longer describe the community package as
  "planned": `Vouchfx.Community.JsonRpc` is live on NuGet.org, the NuGet path leads the consumption
  guidance, and the `ledger-jsonrpc` sample is credited as the canonical consumer.

## 2026-07-10

### Changed

- **`Vouchfx.Community.*` rename + SDK repin (#17)** — the community provider namespace and package
  ID moved from `Community.Steps.*` to `Vouchfx.Community.*`, following the engine's `Platform.*` →
  `Vouchfx.*` rebrand; the hub's SDK pin advanced to the published `Vouchfx.Sdk 1.0.0-alpha.4`
  (#16 prepared the repin and the dual-lane conformance CI).

### Added

- **First community package published to NuGet.org** — `rpc.json-rpc` shipped through the full
  tag-driven Trusted Publishing pipeline, first under the pre-rebrand ID
  (`Community.Steps.JsonRpc/v1.0.0-alpha.1`), then re-published under the current ID as
  [`Vouchfx.Community.JsonRpc` 1.0.0-alpha.1](https://www.nuget.org/packages/Vouchfx.Community.JsonRpc).

## 2026-07-09

### Changed

- **Governance collapsed to two tiers + the Vouched badge (#13)** — Core / Community replace the
  earlier three-tier model; the retired Verified tier's endorsement role moves to the
  maintainer-awarded **Vouched badge** (registry metadata: `vouched` + `vouchedVersion`), with the
  published rubric in `VOUCHED_CHECKLIST.md`.
- **Drift-audit truth-up (#15)** — SDK-timing claims and the shipped publish pipeline documented
  accurately.

### Added

- **Per-provider NuGet packaging (#14)** — pack gate plus the tag-driven `publish-provider.yml`
  Trusted Publishing workflow (ancestry gate, registry-governance gate, nuget.org resolvability
  preflight).
- **Community source submissions opened (#12)** — community-tier providers can be contributed as
  source into `community/` (author-owned, hygiene-gated, hosting ≠ endorsement).

## 2026-07-08

### Changed

- **Registry model explained up front (#9)** — "the JSON is a catalogue entry, not the provider".
- **Provider counts trued-up to the twenty-five-provider engine (#7)** — eleven families, thirteen
  dependency types; consumption-model truth-up after the engine's first alpha releases (#10).

## 2026-07-07

### Changed

- **`rpc.json-rpc` repositioned as the first Community-tier provider (#6)** — moved out of the
  samples lane into `community/`, registered in the index, and adopted as the worked reference for
  the authoring guide.

### Added

- **GitHub Pages site (#5)** — the rendered hub site at
  [tomas-rampas.github.io/vouchfx-providers](https://tomas-rampas.github.io/vouchfx-providers/),
  including the implementing-a-provider guide. The `rpc.json-rpc` provider itself first arrived as
  a sample the day before (#4).

## 2026-06-29

### Added

- **Hub launch** — public repository with the schema-validated community registry
  (`registry/community-providers.json` + JSON Schema), the conformance CI lane for hub-hosted
  providers, the copyable `template/Vouchfx.Community.Hello` scaffold, the implementing-a-provider
  guide, and the governance set (CONTRIBUTING, GOVERNANCE, issue templates). Engine alignment and
  SECURITY.md followed in the first week (#1, #2).
