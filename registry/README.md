# Community Provider Registry

This directory contains the **community provider index** — a curated registry of Community-tier and Verified-tier providers authored by the vouchfx community.

## About the Registry

The registry is stored in two files:

- **`community-providers.json`** — the data file (array of provider entries)
- **`community-providers.schema.json`** — the JSON Schema (draft 2020-12) that validates entries

The registry is human-readable and machine-consumable. It powers:
- The project website's provider listing page
- Automated tools that discover and install providers
- Community feedback and discoverability

## How to Add an Entry

### Option 1: Submit a Pull Request

1. Fork this repository
2. Edit `community-providers.json` to add a new entry (see schema below)
3. Open a pull request with a clear title and description
4. Ensure your entry validates against `community-providers.schema.json` (see validation)

### Option 2: Open an Issue

Click [**New Issue → Provider Listing**](../.github/ISSUE_TEMPLATE/provider-listing.yml) and fill in the form. A maintainer will review and add your provider to the registry.

## Entry Schema

Each provider entry in `community-providers.json` is a JSON object with the following fields:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | The provider's human-readable name (e.g. "Snowflake Assertion", "Redis Pub/Sub") |
| `stepKindId` | string | Yes | The step type identifier in the form `<family>.<provider>` (e.g. `db-assert.snowflake`, `mq-publish.redis`) |
| `repo` | string | Yes | URL to the provider's repository (e.g. `https://github.com/myorg/vouchfx-snowflake-provider`) |
| `nuget` | string | Yes | NuGet package identifier (e.g. `MyOrg.Steps.Snowflake`). Must be the exact package id on NuGet.org. |
| `author` | string | Yes | The provider's author or organisation (e.g. "Acme Corp", "Jane Doe") |
| `minEngineVersion` | string | Yes | Minimum vouchfx engine version required (SemVer format, e.g. `"1.0.0"`) |
| `tier` | enum | Yes | The governance tier: `"community"` or `"verified"` |
| `description` | string | Yes | A one-line summary of the provider's purpose (e.g. "Asserts state in a Snowflake data warehouse") |

### Example Entry

```json
{
  "name": "Snowflake Assertion",
  "stepKindId": "db-assert.snowflake",
  "repo": "https://github.com/acme-corp/vouchfx-snowflake-provider",
  "nuget": "AcmeCorp.Steps.SnowflakeAssert",
  "author": "Acme Corporation",
  "minEngineVersion": "1.0.0",
  "tier": "community",
  "description": "Asserts state in a Snowflake data warehouse using SQL queries"
}
```

### Field Rules

- **`name`** — between 3 and 100 characters; should be descriptive and match the provider's purpose
- **`stepKindId`** — must follow the pattern `<family>.<provider>` where family and provider are lowercase alphanumeric with hyphens (e.g. `db-assert`, `mq-publish`, `cache-get`)
- **`repo`** — must be a valid HTTPS URL to a public repository (GitHub, GitLab, Gitea, etc.)
- **`nuget`** — must be the exact package identifier on NuGet.org (case-sensitive); the package should be public and publicly resolvable
- **`author`** — between 3 and 100 characters; should clearly identify the author or organisation
- **`minEngineVersion`** — must be valid SemVer (e.g. `"1.0.0"`, `"1.1.0"`)
- **`tier`** — one of `"community"` or `"verified"`; only maintainers can promote providers to `"verified"`
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
2. Update the relevant fields in `community-providers.json` (e.g. update `minEngineVersion`, `description`, or move from `"community"` to `"verified"`)
3. Open a pull request
4. A maintainer will review and merge if the changes are consistent with the provider's actual state

## Verification

When a new entry is added or updated, the maintainers verify:
- The NuGet package exists and is publicly resolvable
- The repository URL is valid and accessible
- The entry validates against the schema
- The `stepKindId` does not conflict with existing entries (duplicates are rejected)

## Tier Transitions

- **Community → Verified:** A provider moves from `community` to `verified` when the author submits a pull request to the `verified/` folder, meets the Verified-tier rubric, and the maintainers approve. The registry entry is then updated by a maintainer.
- **Verified → Community:** A provider may be downgraded only if it violates the Verified contract (e.g. a high-severity security issue). The maintainers will notify the author before downgrading.

## Questions?

- **Can I list multiple step types for one provider?** Create separate entries for each step type (e.g. `db-assert.snowflake` and `db-seed.snowflake`).
- **Can I remove my provider from the registry?** Yes, open an issue or a PR requesting removal. Once removed, the entry is archived in git history but no longer listed.
- **What if my NuGet package is not on NuGet.org?** List it in Community tier only if it is publicly resolvable (self-hosted feed, GitHub Packages, etc.). Update the `nuget` field with the full package identifier and any special installation instructions in your repository's README.

---

*This registry is the source of truth for vouchfx provider discovery. Entries are validated against the JSON Schema and verified for consistency with the actual provider repositories.*
