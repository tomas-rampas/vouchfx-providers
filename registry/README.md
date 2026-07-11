# Community Provider Registry

This directory contains the **community provider index** — a curated registry of Community-tier providers authored by the vouchfx community.

## How the model works — the JSON is a catalogue entry, not the provider

A common first-read confusion, resolved up front: a community provider consists of **two entirely different artifacts**, and only one of them is JSON.

1. **The provider itself is C# source, shipped as a NuGet package.** It is a small project implementing the frozen `Vouchfx.Sdk` contract (`IStepProvider`, `IStepBinder<T>`, `IStepValidator<T>`, `IStepCompiler<T>`), compiled and published under Apache-2.0. Providers are compile-time, source-level plugins: the package is the distribution artifact — you add it as a NuGet `PackageReference` in a custom runner and rebuild the host (for published providers), or build from source for unpublished ones; either way the provider is compiled into the host, never loaded at runtime. When the planned provider directory loader ships, packages will become runtime-loadable. The [implementing-a-provider guide](../docs/implementing-a-provider.md) covers writing one end to end.
2. **The registry entry is one JSON object** in the shared `community-providers.json` file below — pure *discovery metadata*: the provider's name, its step kind, where its source lives, which NuGet package to install, the minimum engine version, and whether it holds the Vouched badge. Nobody publishes "a JSON file" as their provider; an author adds a single entry to this shared catalogue so users can find the package.

A Community-tier provider can live in **either of two first-class places** — every provider gets a registry entry here regardless:

1. **Your own repository** (`"hosting": "external"`, the default): write the provider following the guide, publish it as an Apache-2.0 NuGet package, and add your entry with the `nuget` field pointing at your package.
2. **This repository's `community/` directory** (`"hosting": "hub"`): contribute the provider *as source* in a pull request — no NuGet account needed. Your PR adds `community/<YourProvider>/` (+ its `.Tests` sibling) plus the registry entry; CI discovers and runs your tests automatically, and you remain the owner of your folder. The merge bar is licence (Apache-2.0), DCO, namespace hygiene and a green conformance lane — **not** a code review, and **hosting here is not endorsement** (that is what the Vouched badge is for). See [CONTRIBUTING](../CONTRIBUTING.md) and the [community submission PR template](../.github/PULL_REQUEST_TEMPLATE/community-submission.md).

The first entry — [`rpc.json-rpc`](../community/Vouchfx.Community.JsonRpc/README.md) — is hub-hosted and doubles as the worked reference for both the guide and the submission shape.

Today, a published community provider is consumed as a NuGet package referenced from a small custom runner ([`ledger-jsonrpc`](https://github.com/tomas-rampas/vouchfx-samples/tree/main/samples/ledger-jsonrpc) is the worked example); a provider without a published package is consumed via a source-level build — clone the provider's repository (or this one, for hub-hosted providers), reference its project in your build, and rebuild your host. A one-command install experience (`vouchfx providers install <package>`) is planned and tracked on the [engine's public roadmap](https://tomas-rampas.github.io/vouchfx/docs/roadmap.html).

One neighbouring thing this registry is *not*: the [vouchfx-samples](https://github.com/tomas-rampas/vouchfx-samples) repository hosts sample *applications* (systems under test in C#, Python and Java) with complete `.e2e.yaml` suites demonstrating how the engine tests them. It also includes the canonical worked example of consuming a community provider from this registry: [`ledger-jsonrpc`](https://github.com/tomas-rampas/vouchfx-samples/tree/main/samples/ledger-jsonrpc), which depends on `Vouchfx.Community.JsonRpc` from NuGet.

## About the Registry

The registry is stored in two files:

- **`community-providers.json`** — the data file (array of provider entries)
- **`community-providers.schema.json`** — the JSON Schema (draft 2020-12) that validates entries

The registry is human-readable and machine-consumable. It powers:
- Community discoverability — this rendered page and the JSON file itself are the browsable index
- Planned tooling for provider discovery and installation, and a generated provider-listing page on the
  project website (both part of the engine roadmap)

The first entry is [`rpc.json-rpc`](../community/Vouchfx.Community.JsonRpc/README.md) — the reference Community-tier provider, hosted in this repository under `community/`. Its NuGet package [`Vouchfx.Community.JsonRpc`](https://www.nuget.org/packages/Vouchfx.Community.JsonRpc) (version 1.0.0-alpha.1) is published from this repository's CI pipeline and ready to consume.

## How to Add an Entry

### Option 1: Submit a Pull Request (entry only — external hosting)

1. Fork this repository
2. Edit `community-providers.json` to add a new entry (see schema below)
3. Open a pull request with a clear title and description
4. Ensure your entry validates against `community-providers.schema.json` (see validation; CI validates it too)

### Option 2: Submit your provider as source (hub hosting)

Open a pull request that adds `community/<YourProvider>/` (+ its `.Tests` project) **and** your registry entry with `"hosting": "hub"` in one go — use the [community submission template](../.github/PULL_REQUEST_TEMPLATE/community-submission.md) and see [CONTRIBUTING](../CONTRIBUTING.md) for the checklist. No NuGet package required.

### Option 3: Open an Issue

Click [**New Issue → Provider Listing**](../.github/ISSUE_TEMPLATE/provider-listing.yml) and fill in the form. A maintainer will review and add your provider to the registry.

## Entry Schema

Each provider entry in `community-providers.json` is a JSON object with the following fields:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | The provider's human-readable name (e.g. "Snowflake Assertion", "MQTT Publish") |
| `stepKindId` | string | Yes | The step type identifier in the form `<family>.<provider>` (e.g. `db-assert.snowflake`, `mq-publish.mqtt`). Must not collide with a Core provider's step kind (e.g. `mq-publish.redis` is Core and therefore taken) |
| `repo` | string | Yes | URL to the provider's repository (e.g. `https://github.com/myorg/vouchfx-snowflake-provider`; this repository's URL for hub-hosted providers) |
| `hosting` | enum | No | Where the source lives: `"external"` (your repository — the default when absent) or `"hub"` (contributed as source under `community/`) |
| `nuget` | string | Yes for external; optional until published for hub-hosted | NuGet package identifier (e.g. `MyOrg.Steps.Snowflake`). For external providers, must be the exact package id on NuGet.org. For hub-hosted providers, this field is optional unless you plan to publish to NuGet; if publishing, it is required and must equal the provider directory name (e.g. `Vouchfx.Community.JsonRpc` for `community/Vouchfx.Community.JsonRpc`). The publish workflow requires this field to cut a release tag. |
| `author` | string | Yes | The provider's author or organisation (e.g. "Acme Corp", "Jane Doe") |
| `minEngineVersion` | string | Yes | Minimum vouchfx engine version required (SemVer format, e.g. `"1.0.0"`) |
| `vouched` | boolean | No | Maintainer-awarded Vouched badge (true/false). Absence means not vouched. Only maintainers may set this field (gated by CODEOWNERS on /registry/). |
| `vouchedVersion` | string | Yes, if `vouched` is true | The provider version (NuGet package version for external providers, or commit SHA for hub-hosted) that was validated and passed the Vouched rubric review. The badge attests to that specific version only. Maintainer-set only. |
| `description` | string | Yes | A one-line summary of the provider's purpose (e.g. "Asserts state in a Snowflake data warehouse") |

### Example Entry

A fictional entry showing the normal shape — source in the author's own repository, package on NuGet.org (without the Vouched badge):

```json
{
  "name": "Snowflake Assertion",
  "stepKindId": "db-assert.snowflake",
  "repo": "https://github.com/acme-corp/vouchfx-snowflake-provider",
  "nuget": "AcmeCorp.Steps.SnowflakeAssert",
  "author": "Acme Corporation",
  "minEngineVersion": "1.0.0",
  "description": "Asserts state in a Snowflake data warehouse using SQL queries"
}
```

An example entry with the Vouched badge (fictional):

```json
{
  "name": "Snowflake Assertion",
  "stepKindId": "db-assert.snowflake",
  "repo": "https://github.com/acme-corp/vouchfx-snowflake-provider",
  "nuget": "AcmeCorp.Steps.SnowflakeAssert",
  "author": "Acme Corporation",
  "minEngineVersion": "1.0.0",
  "vouched": true,
  "vouchedVersion": "1.2.0",
  "description": "Asserts state in a Snowflake data warehouse using SQL queries"
}
```

For a live example, see the first entry in [`community-providers.json`](community-providers.json) — the hub-hosted `rpc.json-rpc` reference provider (`"hosting": "hub"`, with its `repo` field pointing at this repository). The `rpc.json-rpc` entry has no `vouched` field; it has not yet gone through the Vouched badge review process. The registry field `nuget` for this provider is `Vouchfx.Community.JsonRpc`.

### Field Rules

- **`name`** — between 3 and 100 characters; should be descriptive and match the provider's purpose
- **`stepKindId`** — must follow the pattern `<family>.<provider>` where family and provider are lowercase alphanumeric with hyphens (e.g. `db-assert`, `mq-publish`, `cache-get`)
- **`repo`** — must be a valid HTTPS URL to a public repository (GitHub, GitLab, Gitea, etc.)
- **`nuget`** — must be the exact package identifier on NuGet.org (case-sensitive); the package should be public and publicly resolvable
- **`author`** — between 3 and 100 characters; should clearly identify the author or organisation
- **`minEngineVersion`** — must be valid SemVer (e.g. `"1.0.0"`, `"1.1.0"`)
- **`vouched`** — optional boolean; only maintainers can set this field (gated by CODEOWNERS on /registry/); absence means not vouched
- **`description`** — between 10 and 200 characters; a single-line summary (no linebreaks)

## Validation

Before submitting an entry, validate it against the schema:

### Using `ajv` (CLI)

```bash
npm install -g ajv-cli
ajv validate -s registry/community-providers.schema.json -d registry/community-providers.json
```

### Using a JSON Schema IDE

- **VS Code:** Install the [RedHat YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) and open `community-providers.json`; validation is automatic
- **Online:** Use https://www.jsonschemavalidator.net/ and paste the schema and data

### Programmatically

Any JSON Schema library can validate. Example in .NET (using JsonEverything):

```csharp
using Json.Schema;
using System.Text.Json.Nodes;

var schema = JsonSchema.FromFile("community-providers.schema.json");
var instance = JsonNode.Parse(File.ReadAllText("community-providers.json"));
var result = schema.Evaluate(instance);

if (result.IsValid)
    Console.WriteLine("Valid!");
else
    foreach (var error in result.Errors)
        Console.WriteLine($"Error: {error.InstanceLocation} — {error.Message}");
```

## Updating an Entry

If you own a provider that is already listed:

1. Fork this repository
2. Update the relevant fields in `community-providers.json` (e.g. update `minEngineVersion`, `description`, or `repo`)
3. Open a pull request
4. A maintainer will review and merge if the changes are consistent with the provider's actual state

**To request the Vouched badge:** do not edit the `vouched` field in the registry yourself. Instead, open a [**Vouched request** issue](../.github/ISSUE_TEMPLATE/vouched-request.yml) with evidence of all six rubric items. A maintainer will review and, if approved, open a one-line registry PR to set `vouched: true` with the supporting evidence link.

## Verification

When a new entry is added or updated, the maintainers verify:
- For external entries: the NuGet package exists and is publicly resolvable
- For hub-hosted entries: the source under `community/` builds and its conformance lane is green (when a `nuget` field is present, a release tag cannot be cut until the provider's dependencies — particularly `Vouchfx.Sdk` — are publicly resolvable from NuGet.org; e.g. `rpc.json-rpc`'s package shipped from hub CI once the engine had published `Vouchfx.Sdk` at the pinned version)
- The repository URL is valid and accessible
- The entry validates against the schema (CI enforces this on every PR)
- The `stepKindId` does not conflict with existing entries or a Core provider (duplicates are rejected)

## Badge and Tier Transitions

### Earning the Vouched Badge

A provider earns the Vouched badge by meeting the published rubric in [`VOUCHED_CHECKLIST.md`](../VOUCHED_CHECKLIST.md):

**Path:**
1. Provider is listed in Community tier
2. Author opens a **Vouched request** issue with evidence of all six rubric items
3. A maintainer reviews the issue (conformance, security, CSX, etc.)
4. On success, the maintainer opens a one-line registry PR setting `"vouched": true`
5. PR merges, badge appears in the registry

See [`GOVERNANCE.md`](../GOVERNANCE.md) for the full flow.

### Badge Revocation

A maintainer may revoke the Vouched badge if a provider violates the rubric (e.g. high-severity CVE). The maintainer will notify the author before taking action.

### Promotion to Core

A provider promoted to Core leaves the community registry entirely — Core providers are engine-repository citizens, never registry entries. Promotion decisions are made by the platform team and announced via the engine repository.

## Questions?

- **Can I list multiple step types for one provider?** Create separate entries for each step type (e.g. `db-assert.snowflake` and `db-seed.snowflake`).
- **Can I remove my provider from the registry?** Yes, open an issue or a PR requesting removal. Once removed, the entry is archived in git history but no longer listed.
- **What if my NuGet package is not on NuGet.org?** List it in Community tier only if it is publicly resolvable (self-hosted feed, GitHub Packages, etc.). Update the `nuget` field with the full package identifier and any special installation instructions in your repository's README.

---

*This registry is the source of truth for vouchfx provider discovery. Entries are validated against the JSON Schema and verified for consistency with the actual provider repositories.*
