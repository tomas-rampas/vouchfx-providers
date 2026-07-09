<!--
Community-tier SOURCE submission — a provider contributed into this repository's
community/ directory. Use this template when your PR adds community/<YourProvider>/
(+ its .Tests sibling) and a registry entry.

The merge bar is HYGIENE, not review: licence, DCO, namespace rules, and a green
conformance lane. Merging your provider is NOT an endorsement — that is what the
Verified tier's rubric review is for. You remain the owner of your provider's
folder (a CODEOWNERS line is added at merge).
-->

## Provider

- **Step kind** (`family.provider`): <!-- e.g. mq-publish.mqtt -->
- **What it does** (one line):
- **Folder**: `community/<YourProviderName>/` + `community/<YourProviderName>.Tests/`

## Submission checklist

- [ ] **Licence** — Apache-2.0; every source file carries the licence header.
- [ ] **DCO** — every commit is signed off (`git commit -s`).
- [ ] **Namespace hygiene** — the provider does **not** declare `Platform.Engine.*` or
      `Platform.Steps.*` (reserved; the engine refuses them at startup). The
      `Community.Steps.<Name>` convention is recommended.
- [ ] **No Core collision** — the step kind is not already taken by a Core provider or an
      existing registry entry.
- [ ] **Builds standalone** — the project and its tests restore/build against the packed SDK
      (`packages-local/` feed via `nuget.config`); no engine-repo checkout required beyond
      what the conformance workflow itself does. Not being in the `.sln` is fine — CI
      discovers `community/**/*.Tests.csproj` by glob.
- [ ] **Conformance** — the SDK conformance harness (`Platform.Sdk.Testing`) passes locally;
      unit tests cover bind/validate/emit and the four-verdict mapping.
- [ ] **§13.3.1 CSX rules** — provider-prefixed helper class, brace-enclosed statement block,
      namespace-only `RequiredUsings`, no `using var`, ids sanitised before splicing.
- [ ] **Registry entry** — added to `registry/community-providers.json` with
      `"hosting": "hub"` and `"tier": "community"` (the `nuget` field is optional for
      hub-hosted entries) and validates against the schema.
- [ ] **README** — the provider folder has a README that opens with the Community-tier
      notice (unreviewed, unendorsed; use at your own judgement) and documents every step
      field.

## Anything a reviewer should know

<!-- Design notes, known limitations, dependencies on managed dependency types, etc. -->
