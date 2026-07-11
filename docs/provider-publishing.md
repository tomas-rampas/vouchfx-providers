# Publishing and Submission

**Stage 6 of the provider authoring journey**

Once your provider is tested and documented, you have two hosting options for listing in the Community tier, plus the optional post-listing Vouched badge review.

## Community Tier — External Hosting

**When to choose this:** You want to keep the source in your own repository and publish it yourself. You want discoverability without seeking platform endorsement.

### How to Submit

1. **Publish to NuGet** — pack your provider as a NuGet package and push it to nuget.org:

   ```bash
   dotnet pack your-provider/your-provider.csproj -c Release -o ./nupkg
   dotnet nuget push nupkg/YourOrg.Steps.YourKind.1.0.0.nupkg -k <api-key> -s https://api.nuget.org/v3/index.json
   ```

2. **Add to the community index** — open a GitHub issue using the **Provider Listing** template, or submit a pull request to `registry/community-providers.json` following the schema in `registry/community-providers.schema.json`.

3. **List immediately** — a maintainer adds your provider to the registry. There is no conformance testing for externally-hosted Community providers; only Apache-2.0 compliance and the reflective-discovery contract.

## Community Tier — Hub Hosting

**When to choose this:** You do not have (or want) a NuGet account, or you want your provider's tests running in the hub's CI. Hosting here is **not** endorsement — the merge bar is hygiene (Apache-2.0, DCO, namespace rules, green conformance lane), not a code review.

### How to Submit

Open one pull request adding `community/<YourProvider>/` + `community/<YourProvider>.Tests/` **and** your registry entry with `"hosting": "hub"` (the `nuget` field is optional). Use the community submission PR template; CI discovers `community/**/*.csproj` by glob and runs your tests in their own step. The full step-by-step lives in `CONTRIBUTING.md`; the `rpc.json-rpc` provider under `community/` is the worked example of this shape.

## The Vouched Badge — Platform Endorsement

**When to pursue this:** Your Community provider (hosted either externally or in the hub) is already listed in the registry and passes its conformance tests. You want to work towards the optional Vouched badge — platform-team review and endorsement.

### The Rubric

1. Integration-test fixture passes on the engine main branch plus two preceding minors
2. README with ≥3 worked examples plus a known-limitations section
3. Security sign-off: credentials, transitive CVEs, TLS, no telemetry, package signature
4. Apache-2.0 licence plus all commits signed off via DCO (`git commit -s`)
5. `MinEngineVersion` declared in provider metadata
6. CSX code reviewed for composition-rule conformance by a maintainer

### How to Request the Badge

1. **Your provider is already listed** — the provider appears in the registry and (if hub-hosted) CI is passing.

2. **Open a Vouched request issue** — use the **Vouched Request** issue template. Link to your provider source and confirm which rubric items are met. Maintainers will prioritise review bandwidth and give you early feedback.

3. **Maintainer review:**
   - **Security review** — credentials, CVEs, TLS, telemetry, package signature
   - **CSX review** — read the generated C# code; confirm it follows the composition rules
   - **Conformance validation** — verify your fixture passes on engine main plus two preceding minors (human validation; CI runs only main)

4. **Badge award** — upon approval, a maintainer opens a one-line registry PR adding `"vouched": true` to your entry. Once merged, the badge is live on your listing. Your provider remains where it is — hub-hosted or externally hosted — with the platform team's endorsement now visible in the registry.

## Submission Checklist

Before opening a Community submission or requesting the Vouched badge:

- [ ] **Namespace:** My provider uses a non-reserved namespace (never `Vouchfx.Engine.*` or `Vouchfx.Steps.*`)
- [ ] **Four interfaces:** My provider implements `IStepProvider`, `IStepBinder<TModel>`, `IStepValidator<TModel>`, `IStepCompiler<TModel>`
- [ ] **Model:** My step model is a strongly-typed record, never `Dictionary<string,object>`
- [ ] **CSX composition:**
  - [ ] `RequiredUsings` is bare namespace strings only
  - [ ] `RequiredHelpers` contains one provider-id-prefixed nested static class
  - [ ] `StatementBlock` is one brace-enclosed block, built as a `$$"""…"""` raw string
  - [ ] No `using var` in the body
  - [ ] Step id is sanitised via `CsxFragment.SanitiseId`
  - [ ] Cross-step state passes only through `Vars`
- [ ] **Verdicts:** My provider maps exceptions to the four-outcome taxonomy (Pass, Fail, EnvironmentError, Inconclusive)
- [ ] **Secrets:** Every author-facing string field is resolved via `Secret_Helpers.ResolveTemplate`; secrets never appear in observations
- [ ] **Capture:** My provider evaluates `capture` expressions against the response and writes results to `Vars`
- [ ] **References:** If my emitted code calls types outside System.*, I implement `ICompileReferenceContributor`
- [ ] **Tests:**
  - [ ] Conformance tests drive the full pipeline (schema-validate → parse → bind → validate → emit → compile → execute)
  - [ ] At least one Pass path and one Fail path are tested
  - [ ] Unit tests confirm CSX composition rules are satisfied
  - [ ] Integration tests (if applicable) run Docker fixtures
  - [ ] All tests pass: `dotnet test -c Release --filter "requires!=docker"`
- [ ] **Documentation:**
  - [ ] `README.md` exists and contains ≥3 worked `.e2e.yaml` examples
  - [ ] Known limitations are documented
  - [ ] The provider's step type, model shape, and typical use cases are clear
- [ ] **Licensing:** My provider is Apache-2.0 licensed (or compatible)
- [ ] **Commits:** All commits are signed off via DCO: `git commit -s`
- [ ] **Build:** My provider builds with zero warnings: `dotnet build /p:TreatWarningsAsErrors=true`
- [ ] **Metadata:** `MinEngineVersion` is declared in provider metadata

For Vouched badge requests:

- [ ] I have read the CSX composition rules and confirmed my provider follows them
- [ ] I have read the security review checklist (VOUCHED_CHECKLIST.md)
- [ ] My fixture passes locally and on the engine main branch

---

**Welcome to the vouchfx provider ecosystem.** Thank you for contributing.

**Next:** [Community Registry](../registry/README.md) — how providers are listed and discovered · [Governance](../GOVERNANCE.md) — tier model and badge policy · [VOUCHED_CHECKLIST](../VOUCHED_CHECKLIST.md) — the detailed Vouched rubric.
