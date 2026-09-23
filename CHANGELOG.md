# Changelog

All notable changes to the vouchfx provider hub are documented in this file.

The format follows the spirit of [Keep a Changelog](https://keepachangelog.com/). The hub repository
itself is not versioned — entries are dated milestones. The provider packages the hub publishes carry
their own semantic versions via tags of the form `<Provider>/vX.Y.Z` (e.g.
`Vouchfx.Community.JsonRpc/v1.0.0-alpha.1`), and each package's releases are the authoritative record
for that provider.

## [Unreleased]

### Changed

- **Published SDK pin advanced to `1.0.0-rc.6`.** `$(VouchfxSdkVersion)` in
  `Directory.Build.props` moves from `1.0.0-rc.5` to `1.0.0-rc.6`
  (`93287ffbb0623ba253816ed4d909f50e1b26da93`), so hub-hosted providers now build and publish
  against this engine release. Both hub-hosted provider projects — the `Vouchfx.Community.Hello`
  template and the published `Vouchfx.Community.JsonRpc` — restore, build and test unchanged
  against the new pin, with no source edit needed in either: 29/29 tests passed, 0 build
  warnings, the format gate stayed clean, and the pack gate that validates every hub provider's
  `.nupkg` metadata also stayed green. Because both providers directly implement the frozen v1
  `IStepProvider`/`IStepBinder<T>`/`IStepValidator<T>`/`IStepCompiler<T>`/`IResourceContributor<T>`
  surface, that unmodified green build is itself evidence the contract held across the bump — no
  interface member moved in a way that would break an existing implementor. Per the engine's own
  release notes for this tag, the one provider-relevant item is PR #465, landing the CSX helper
  `Source`-body freeze golden (`vouchfx-sdk-helper-sources.v1.txt`) that the entry below noted
  would land "after rc.5"; it is inert here, since the hub's only published provider splices
  `SecretHelper` alone and never `KafkaSecurityHelper`. The out-of-repo literals that cannot
  inherit the property move with it: the conditioned fallbacks in the two
  `template/Vouchfx.Community.Hello*` csprojs, the `Vouchfx.Sdk` samples in
  `docs/provider-project-setup.md` and `docs/consuming-a-provider.md`,
  `site/facts-fallback.json`'s `engine_release`/`sdk_version`, and the pin note in
  `.github/workflows/publish-provider.yml`. The engine-main lane's checkout `ref:` in
  `.github/workflows/conformance.yml` moves to the engine's current `main` rather than to the tag
  (`1c94af80c7510c0ed1889cba6ecb792d1612a60b`, four commits later), as that workflow's repin
  procedure requires, so the lane keeps tracking what comes next instead of what was just
  released. The SDK remains a pre-release, so NuGet's NU5104 rule is unchanged: provider release
  tags must still carry pre-release versions.
- **Published SDK pin advanced to `1.0.0-rc.5`.** `$(VouchfxSdkVersion)` in
  `Directory.Build.props` moves from `1.0.0-rc.4` to `1.0.0-rc.5`
  (`cc5e8efa9c84f59e1135568456f7c156261f6263`), so hub-hosted providers now build and publish
  against the engine release that finished encrypted-client-key mutual TLS support for Kafka
  (`security.clientKeyPassword`, engine #406). The frozen v1 provider-implemented SDK contract is
  unchanged between the two tags — `StepContract.cs` and both the `Vouchfx.Sdk`/
  `Vouchfx.Sdk.Testing` public-API goldens are byte-identical, so no interface, default-implemented
  context member, or signature moved, and no hub provider needs a code change. The one real change
  in the SDK closure is a body-only edit to `KafkaSecurityHelper`'s CSX `Source` (it now also reads
  `ClientKeyPassword` and sets `SslKeyPassword` on the `mtls` branch); the per-body-hash golden that
  would gate such a change (`vouchfx-sdk-helper-sources.v1.txt`) lands after rc.5, and no
  hub-hosted provider references `KafkaSecurityHelper` today (`Vouchfx.Community.JsonRpc` splices
  only `SecretHelper`, which is untouched), so this is inert here. The out-of-repo literals that
  cannot inherit the property move with it: the conditioned fallbacks in the two
  `template/Vouchfx.Community.Hello*` csprojs, the `Vouchfx.Sdk` samples in
  `docs/provider-project-setup.md` and `docs/consuming-a-provider.md`,
  `site/facts-fallback.json`'s `engine_release`/`sdk_version`, the engine-main lane's checkout
  `ref:` in `.github/workflows/conformance.yml`, and the pin note in
  `.github/workflows/publish-provider.yml`. The SDK remains a pre-release, so NuGet's NU5104 rule
  is unchanged: provider release tags must still carry pre-release versions.
- **Published SDK pin advanced to `1.0.0-rc.4`.** `$(VouchfxSdkVersion)` in
  `Directory.Build.props` moves from `1.0.0-rc.1` to `1.0.0-rc.4`, so hub-hosted providers now
  build and publish against the engine release that added TLS and mutual TLS for the
  infrastructure a suite talks to. The v1 provider-implemented SDK interfaces are unchanged —
  the additions in this release (`IProjectContext.DeclaredServices`,
  `ICompileContext.DeclaredServices`) are engine-supplied and provider-consumed, so no provider
  needs a code change. The out-of-repo literals that cannot inherit the property move with it:
  the conditioned fallbacks in the two `template/Vouchfx.Community.Hello*` csprojs and the
  `Vouchfx.Sdk` samples in `docs/provider-project-setup.md` and `docs/consuming-a-provider.md`
  (the latter two had drifted to `1.0.0-alpha.9`). The SDK remains a pre-release, so NuGet's
  NU5104 rule is unchanged: provider release tags must still carry pre-release versions.
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
  [providers.vouchfx.io](https://providers.vouchfx.io/),
  including the implementing-a-provider guide. The `rpc.json-rpc` provider itself first arrived as
  a sample the day before (#4).

## 2026-06-29

### Added

- **Hub launch** — public repository with the schema-validated community registry
  (`registry/community-providers.json` + JSON Schema), the conformance CI lane for hub-hosted
  providers, the copyable `template/Vouchfx.Community.Hello` scaffold, the implementing-a-provider
  guide, and the governance set (CONTRIBUTING, GOVERNANCE, issue templates). Engine alignment and
  SECURITY.md followed in the first week (#1, #2).
