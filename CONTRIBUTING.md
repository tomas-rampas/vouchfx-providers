# Contributing a Provider to vouchfx

This guide explains how to submit a provider for listing in the community index or for Verified-tier endorsement.

## Before You Start

**You should have already written your provider.** This repository is for *listing* and *submitting* providers, not for authoring them. To learn how to write a provider, see:

- **Engine [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md)** — complete guide with the step type model, the provider contract (four required interfaces), hard rules for CsxFragment composition, reserved namespaces, and testing patterns.
- **[`Example.Steps.Echo`](https://github.com/tomas-rampas/vouchfx/tree/main/examples/Example.Steps.Echo)** — a fully documented worked example with friction log.
- **[`Example.Steps.Hello`](https://github.com/tomas-rampas/vouchfx/tree/main/examples/Example.Steps.Hello)** — a minimal copyable template.

## Provider Requirements

All providers in this repository must meet these baseline requirements:

### Licensing and Attribution
- **Apache-2.0 license** (or compatible). The vouchfx engine and all Core providers are Apache-2.0; all community providers must be compatible.
- **Developer Certificate of Origin (DCO) sign-off.** Commits must be signed off with `git commit -s` or the GitHub web UI "Sign off" checkbox. The DCO confirms you have the right to license your work. It is lighter-weight than a Contributor Licence Agreement and is industry standard.

### The Provider Contract
- Your provider must implement the four required interfaces from `Platform.Sdk`: `IStepProvider`, `IStepBinder<TModel>`, `IStepValidator<TModel>`, and `IStepCompiler<TModel>`.
- Your model must be a strongly-typed record (never `Dictionary<string,object>`).
- You must NOT use the reserved namespace prefixes: `Platform.Engine.*` or `Platform.Steps.*`. Use your own namespace (e.g. `MyOrg.Steps.Kafka`, `Community.Steps.Snowflake`).
- Your provider must be discoverable at suite startup via the reflective `StepKindRegistry`. Add the `[StepProvider]` attribute to your provider class; no manual registration is required.

### CsxFragment Composition
When your `IStepCompiler<TModel>` emits a `CsxFragment`, you must follow the strict rules in the engine's architecture blueprint (§13.3.1):

1. **Three fields only:** `RequiredUsings` (bare namespace strings), `RequiredHelpers` (nested static classes prefixed with your provider id), `StatementBlock` (brace-enclosed C# block).
2. **No `using var` in the Roslyn script body.** Use plain `var` + explicit `.Dispose()` in a `finally`.
3. **Emit bodies as C# 11 double-dollar raw strings** (`$$"""…"""`). Single `{`/`}` are literal braces; `{{hole}}` is an interpolation.
4. **Sanitise step ids before splicing.** Call `CsxFragment.SanitiseId(stepId)` to convert hyphens to underscores.
5. **Cross-step state passes only through `Vars`.** Read earlier steps' captured state from `Vars`; write your outcome via `VarKeys.Outcome(safeId)`.

See the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) for worked code examples and the blueprint (§13.3.1) for full details.

### Testing
- You must have a test suite that exercises your provider. Use the `Platform.Sdk.Testing` package:
  - For dependency-free providers: use `ProviderTestHarness.RunSingleStepAsync()` for end-to-end unit tests (no Docker needed).
  - For infrastructure providers: author an integration-test fixture using the vouchfx engine's topology orchestration.
- Local tests must pass: `dotnet test <your-provider.Tests> -c Release --filter "requires!=docker"`.
- If your provider uses infrastructure (databases, brokers), include a Docker integration test. Community providers can author this within their own repository or contribute it to the main vouchfx repository if it uses Core infrastructure.

## Two Submission Paths

### Path 1: Community Tier (Index Listing)

List your provider in the community index **if:**
- Your provider is authored, tested, and published on NuGet
- You want discoverability without seeking platform-team endorsement
- You do not (yet) meet the Verified-tier rubric

**How to submit:**

1. **Option A: Open an issue** — click [**New Issue → Provider Listing**](.github/ISSUE_TEMPLATE/provider-listing.yml) and fill in the form. A maintainer will add your provider to the registry.

2. **Option B: Submit a PR** — fork this repository, add an entry to `registry/community-providers.json` following the schema in `registry/community-providers.schema.json`, and open a pull request. See [`registry/README.md`](registry/README.md) for the field meanings and validation.

There is no conformance testing for Community providers — the gatekeeping is only Apache-2.0 compliance and the reflective-discovery contract. The published Verified-tier rubric is the feedback for what is needed to graduate.

### Path 2: Verified Tier (Platform Endorsement)

Submit for Verified-tier endorsement **if:**
- Your provider meets or nearly meets the [Verified-tier rubric](VERIFIED_TIER_CHECKLIST.md)
- You want platform-team endorsement and website listing
- You are prepared for security review and CSX conformance review

**How to submit:**

#### Step 1: Signal Intent (Optional but Recommended)

Open an issue [**New Issue → Verified Proposal**](.github/ISSUE_TEMPLATE/verified-proposal.yml). This tells maintainers you plan to submit, lets them coordinate review capacity, and gives you early feedback on readiness. It is not mandatory but is strongly recommended.

#### Step 2: Prepare Your Submission

Organise your provider for submission:
- Create a folder at `verified/<your-provider-id>/` (e.g. `verified/snowflake-assert/` or `verified/redis-pubsub/`). The id should be descriptive and match your step type family or provider name.
- Copy your provider's **source code** into `verified/<your-provider-id>/src/` (the minimal subset needed to compile your provider).
- Copy your **integration-test fixture** into `verified/<your-provider-id>/tests/` (the conformance test that CI will run).
- Copy your **README** into `verified/<your-provider-id>/README.md` (or link to it in your submission PR description).
- Ensure your project file references `Platform.Sdk` from the local feed (for pre-v1.0) or NuGet (post-v1.0).

#### Step 3: Open a Pull Request

Fork this repository, commit your submission, and open a PR. **Use the Verified submission pull-request template** (available when you open the PR). Complete the checklist:

- [ ] Provider meets all six Verified-tier rubric items (conformance matrix, README use cases + known limitations, security sign-off, Apache-2.0 + DCO, MinEngineVersion declared, CSX reviewed)
- [ ] Integration-test fixture is included and runs locally: `dotnet test verified/<provider-id>/tests/ -c Release`
- [ ] README contains at least three realistic use cases
- [ ] README includes a known-limitations section
- [ ] Provider is Apache-2.0 licensed
- [ ] All commits are signed off with DCO (`git commit -s`)
- [ ] MinEngineVersion is declared in provider metadata
- [ ] I have read the CsxFragment composition rules (§13.3.1 of the architecture blueprint) and confirmed my provider follows them

#### Step 4: CI Conformance Gate

CI automatically runs when your PR opens:

1. **Compile** — your provider against `Platform.Sdk`
2. **Unit and Integration Tests** — your test suite against the engine `main` branch
3. **Schema Validation** — your provider's JSON Schema fragment is validated

**All tests must pass.** If any fail, push fixes to your PR branch; CI re-runs automatically.

**Note:** CI runs your conformance test against the engine `main` SDK only. The Verified-tier rubric requires you to verify your provider also passes on the engine main branch plus the two preceding minor releases; that multi-version validation is a human-review requirement verified by maintainers during the submission review, not an automated CI step.

#### Step 5: Security Review

A maintainer reviews your provider for:
- **Credential handling** — are credentials stored, transmitted, and used securely? (Never hardcoded, never logged, always over TLS where applicable.)
- **Dependency vulnerabilities** — are transitive dependencies scanned? Zero high-severity CVEs at promotion.
- **TLS defaults** — does your provider enforce TLS where applicable (e.g. database connections, webhooks)?
- **Telemetry** — does your provider phone home or exfiltrate data? (It must not.)
- **Package signature** — is your NuGet package signed?

The security sign-off is a maintainer's responsibility; you provide the information, and the maintainer documents their review.

#### Step 6: CSX Review

A maintainer reads the C# code emitted by your representative steps and confirms it follows the CsxFragment composition rules (§13.3.1). This is a code review of the *generated* code, not your provider implementation — it ensures the CSX you emit is safe to execute in the collectible `AssemblyLoadContext`.

#### Step 7: Merge and Promotion

Upon approval of CI, security review, and CSX review, the maintainer merges your PR. Your provider is now:
- Promoted to Verified tier
- Listed on the vouchfx project website
- Added to the public community-providers registry
- Eligible for inclusion in future platform communications

## Building Before v1.0

The `Platform.Sdk` and `Platform.Sdk.Testing` packages are published to NuGet with the vouchfx v1.0 release. Until then, pack the five SDK-closure projects locally from the engine:

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

This repository's `nuget.config` already points to `packages-local`:

```xml
<add key="local" value="packages-local" />
```

Local builds will consume the locally packed SDK.

## The Verified-Tier Rubric (Full Reference)

For the complete checklist, see [`VERIFIED_TIER_CHECKLIST.md`](VERIFIED_TIER_CHECKLIST.md). Summary:

1. Integration-test fixture passes on main + two preceding minors
2. README with ≥3 use cases + known-limitations section
3. Security sign-off: credentials, transitive CVEs, TLS, no telemetry, signature
4. Apache-2.0 + DCO sign-off
5. MinEngineVersion declared
6. CSX reviewed for §13.3.1 conformance by a maintainer

## Namespace Rules

**Reserved prefixes (you cannot use these):**
- `Platform.Engine.*` — reserved for vouchfx engine code
- `Platform.Steps.*` — reserved for Core providers

**Your provider's namespace must be unique.** Examples:
- `MyOrg.Steps.Kafka`
- `Community.Steps.Snowflake`
- `Example.Steps.Hello`

A unique namespace prevents assembly-graph collisions and makes it clear which team owns the code.

## Questions?

- **How do I write a provider?** See the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) and the worked examples.
- **What is CsxFragment composition?** See the architecture blueprint's section 13.3.1.
- **How do I test my provider?** See the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) under "Testing Your Provider".
- **Can I update my provider after it is listed?** Yes. Community providers are versioned independently on NuGet; update your NuGet package and open a PR to update the registry entry. Verified providers can be updated by opening new PRs to the `verified/` folder.
- **What if I disagree with a maintainer's decision?** Open an issue on this repository or the main vouchfx repository. The platform team is committed to fair, defensible decisions based on the published rubric.

## Licence

All contributions are made under the Apache-2.0 licence and must be compatible with it. By submitting a pull request, you agree to licence your contribution under the Apache-2.0 licence.

---

Thank you for contributing to vouchfx. The community tier model exists because the platform grows through the efforts of contributors like you.
