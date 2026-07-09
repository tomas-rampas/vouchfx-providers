# The Vouched Checklist

A provider is awarded the **Vouched badge** when it meets all six items in this rubric. This checklist is the source of truth for the Vouched-badge endorsement; it is objective, published, and enforced by maintainer review.

The Vouched badge is awarded **post-listing**: a provider must already have a registry entry (listed in `registry/community-providers.json`) before a Vouched request is opened.

## The Six Required Items

### 1. Conformance Matrix: Integration Tests Pass

Your provider's integration-test fixture must pass on the official conformance matrix without failures or flakes.

**Official matrix:**
- Engine main branch (`1.0.0` or later)
- Two preceding minor versions (or all released minor versions, when fewer than two exist), validated by a maintainer during review

**How it is tested:**

*For hub-hosted providers:*
- CI automatically runs your integration-test fixture against the engine `main` branch when the provider source is updated in `community/`.
- All tests must pass (0 failures, 0 skips, 0 flakes).
- A maintainer will verify that your fixture also passes on the engine main branch plus two preceding minor releases.

*For externally hosted providers:*
- You provide a link to a public CI run of your conformance fixture (green run against the engine version your provider targets).
- A maintainer will reproduce and verify the fixture passes on the engine main branch plus two preceding minor releases.

**What to do:**
1. Author an integration-test fixture that exercises your provider end-to-end with live infrastructure (databases, brokers, etc.).
2. If hub-hosted: place it in `community/<your-provider-id>.Tests/` (as a sibling to your provider project).
3. If externally hosted: place it in your own repository (e.g. `tests/ConformanceTests.cs`).
4. Ensure it runs locally: `dotnet test` against your test project (`-c Release`).
5. When you open a Vouched request issue, link to your CI run (external) or confirm your hub-hosted tests are green on `main`.

**Failure mode:** If any test fails on the engine `main` or on the multi-version validation, the maintainer will request changes or ask for evidence of remediation.

---

### 2. Documentation: README with Use Cases and Known Limitations

Your provider's README must be comprehensive and cover real-world scenarios.

**Requirements:**
- At least three realistic, worked use cases covering different aspects of your provider's capability
- A dedicated "known limitations" section that documents edge cases, unsupported configurations, performance characteristics, or deliberate gaps
- Installation and setup instructions (if not obvious from the worked examples)
- Any required configuration or prerequisites (environment variables, permissions, etc.)

**What to do:**
1. Write a README in your provider's repository root or as `README.md` in your provider folder.
2. Include at least three use cases. Each should show:
   - A brief description of the scenario
   - A code example (a YAML `.e2e.yaml` snippet showing the step type in use)
   - The expected outcome
3. Add a "Known Limitations" section. Examples:
   - "This provider requires PostgreSQL 11 or later; earlier versions are not tested."
   - "Timeout handling: if the provider exceeds 30 seconds, the engine's step timeout will fire; see verifyMode for polling strategies."
   - "This provider cannot be used with TLS-disabled connections; TLS is mandatory."
4. If your provider is hub-hosted, the README lives in `community/<your-provider-id>/README.md`. If externally hosted, it lives in your repository.

**Failure mode:** A README with fewer than three use cases or without a known-limitations section will not pass review. The known-limitations section is not a place to list bugs; it is where you document the boundaries of what your provider does and does not do.

---

### 3. Security Sign-Off

A maintainer must sign off on your provider's security posture. This is **not** an automated check; a human reviews the evidence you provide.

**Requirements:**
1. **Credential handling:** Credentials (database passwords, API keys, etc.) must be:
   - Never hardcoded in your provider code or examples
   - Resolved via the vouchfx `Vars.Secrets` mechanism at step execution time
   - Never logged or printed to stdout/stderr
   - Transmitted over TLS where applicable (database connections, HTTP, etc.)
   
   **What to provide:** A brief note describing how credentials are handled (e.g. "Credentials are read from `Vars.Secrets.Resolve(ref)` and passed to Npgsql.NpgsqlConnectionStringBuilder; no secrets are logged.")

2. **Transitive dependency vulnerabilities:** Your provider's NuGet package must have zero high-severity CVEs at the time of promotion.
   
   **What to provide:** Run `dotnet list package --outdated` and confirm there are no vulnerabilities. If you use a vulnerable transitive dependency, document your mitigation (e.g. "Direct dependency X has a CVE in transitive Y, but Y is not reachable in our code path because…").

3. **TLS defaults:** Connections to external services (databases, brokers, APIs) must use TLS by default. If a provider supports unencrypted connections, they must be:
   - Opt-in only (not the default)
   - Documented as insecure
   - Not used in any default code paths or examples
   
   **What to provide:** A note on how TLS is configured (e.g. "PostgreSQL connections default to SSL=Require; unsecured connections are not supported").

4. **Telemetry:** Your provider must **not** phone home or exfiltrate data. No telemetry, analytics, or diagnostic calls to external services.
   
   **What to provide:** A statement such as "This provider makes no outbound connections beyond those required to interact with the target service (PostgreSQL, Kafka, etc.); there are no telemetry or analytics calls."

5. **Package signature (NuGet):** Your NuGet package should be signed (optional but recommended). If you publish an unsigned package, document why.
   
   **What to provide:** A link to your NuGet package and confirmation that it is signed, or an explanation if it is not.

**How security review works:**
1. You provide the evidence above in your Vouched request issue (see `CONTRIBUTING.md` or the [vouched-request issue template](../.github/ISSUE_TEMPLATE/vouched-request.yml)).
2. A vouchfx maintainer reviews your provider code, dependency tree, and examples.
3. If all checks pass, the maintainer approves the security sign-off.
4. If issues are found, the maintainer requests changes (e.g. removing hardcoded secrets, updating a vulnerable dependency, documenting a TLS gap).

**Failure mode:** Unresolved security issues will block the Vouched badge. Common blockers: hardcoded credentials, high-severity transitive CVEs, unencrypted default connections, or telemetry calls.

---

### 4. Licence and DCO Sign-Off

Your provider must be Apache-2.0 licensed (or compatible), and you must sign off via the Developer Certificate of Origin.

**Requirements:**
1. Your provider repository must include a `LICENSE` file with the Apache-2.0 text (or link to the SPDX text).
2. Your provider's `.csproj` must declare the license (optional but recommended):
   ```xml
   <PropertyGroup>
     <License>Apache-2.0</License>
   </PropertyGroup>
   ```
3. All commits in your work must be signed off: use `git commit -s` or the GitHub web UI "Sign off" checkbox. The sign-off text should read:
   ```
   Signed-off-by: Your Name <your.email@example.com>
   ```

**How DCO sign-off works:**
- When you commit with `git commit -s`, Git appends your sign-off line to the commit message.
- When you push a PR or open an issue, a GitHub bot checks that all relevant commits are signed off.
- You can sign off retroactively: `git rebase -i main` and amend each commit to add the sign-off line, then force-push to your PR branch.

**Why DCO matters:**
The DCO confirms that you have the legal right to license your work under Apache-2.0 and that you have read and understood the Developer Certificate of Origin.

**Failure mode:** If any commit is not signed off, the DCO check will flag it and the badge cannot be awarded.

---

### 5. MinEngineVersion Declaration

Your provider must declare which version(s) of the vouchfx engine it is compatible with.

**Requirements:**
1. Your `IStepProvider` implementation must declare a `MinEngineVersion` in the `ProviderMetadata` record.
2. The version should be compatible with the engine's current major version. For example, if the latest engine is `1.0.x`, your `MinEngineVersion` might be `1.0.0` (if you support the current release).
3. Ensure your provider's API surface aligns with the declared version. If you use an interface that was introduced in engine v1.1, set `MinEngineVersion` to `1.1.0`.

**How to declare it:**
In your provider class, implement `IStepProvider` with the frozen v1 contract:

```csharp
public sealed class MyProvider : IStepProvider
{
    public StepKindId Kind { get; } = new StepKindId("my-family", "my-provider");

    public ProviderMetadata Metadata { get; } = new ProviderMetadata(
        Version: "1.0.0",
        MinEngineVersion: "1.0.0",
        License: "Apache-2.0",
        Authors: new[] { "your-name" }
    );
    // ...
}
```

**Failure mode:** A provider without a declared `MinEngineVersion`, or one that conflicts with the actual engine APIs it uses, will not pass review.

---

### 6. CsxFragment Composition Review (§13.3.1 of the Architecture Blueprint)

At least one vouchfx platform-team maintainer must read the C# code emitted by your provider and confirm it adheres to the CsxFragment composition contract.

**What is CsxFragment composition?**
When your `IStepCompiler<TModel>` emits a `CsxFragment`, the code runs inside a collectible `AssemblyLoadContext` and must follow strict rules to prevent collisions and memory leaks.

**The rules (from the architecture blueprint §13.3.1):**

1. **Three fields only:**
   - `RequiredUsings` — bare namespace strings (e.g. `"System"`, `"System.Text.Json"`)
   - `RequiredHelpers` — static helper classes prefixed with your provider id (e.g. `DbAssertPostgres_Helpers`)
   - `StatementBlock` — one brace-enclosed C# block

2. **No `using var` in Roslyn script bodies.** Use `var` + explicit `.Dispose()` in a `finally`:
   ```csharp
   var resource = AcquireResource();
   try { /* use resource */ }
   finally { resource.Dispose(); }
   ```

3. **Emit bodies as C# 11 double-dollar raw strings** (`$$"""…"""`):
   ```csharp
   var block = $$"""
   {
       var message = {{messageLiteral}};
       // Single braces are literal; {{hole}} is interpolated.
   }
   """;
   ```

4. **Sanitise step ids:** Call `CsxFragment.SanitiseId(stepId)` before using ids in variable names:
   ```csharp
   var safeId = CsxFragment.SanitiseId(ctx.StepId); // "my-step-id" → "my_step_id"
   ```

5. **Cross-step state passes only through `Vars`.** Never assume variables declared by another provider will be in scope.

**How review works:**
1. When you open a Vouched request, a maintainer reads the `Emit` method of your `IStepCompiler<TModel>` for at least one representative step (e.g. the primary assertion step if you have multiple step types).
2. The maintainer confirms that the emitted C# code follows all five rules above.
3. If any rule is violated, the maintainer requests changes.
4. Once the emitted code is confirmed to be correct, the review is approved.

**Common issues caught in CSX review:**
- Using `using var` instead of `var` + explicit `Dispose()`
- Single-dollar raw strings (`$"""…"""`) instead of double-dollar (`$$"""…"""`)
- Failing to sanitise step ids before using them in variable names
- Declaring helper classes without the provider-id prefix (collision risk)
- Attempting to share state between steps via static variables or module-level state

**Failure mode:** Code that violates §13.3.1 will be caught in CSX review and must be fixed before the badge is awarded.

**Resource:**
See the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) for worked examples and the architecture blueprint (§13.3.1) for the complete rules.

---

## Vouched Request Checklist

Use this checklist when you open a Vouched request issue to request the badge for your provider:

- [ ] **Provider listed:** My provider is already in `registry/community-providers.json`
- [ ] **Conformance:** Integration-test fixture passes locally and on the conformance matrix (engine main + two preceding minors)
- [ ] **Documentation:** README with ≥3 use cases and known-limitations section
- [ ] **Security:** Credentials reviewed, transitive CVEs scanned, TLS defaults checked, no telemetry, signature noted
- [ ] **Licence:** Apache-2.0 license in provider repository and `.csproj`
- [ ] **DCO:** All commits signed off (`git commit -s`)
- [ ] **MinEngineVersion:** Declared in the provider's `Metadata` property (`ProviderMetadata.MinEngineVersion`)
- [ ] **CSX:** Representative steps reviewed against §13.3.1 (verified by a maintainer during review)

---

## Questions?

- **How do I structure my integration tests?** See the engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) under "Testing Your Provider" and the worked example [`Example.Steps.Echo`](https://github.com/tomas-rampas/vouchfx/tree/main/examples/Example.Steps.Echo).
- **Can I fix issues after submitting a Vouched request?** Yes. Update your provider; the maintainer will re-review.
- **What if I can't meet one rubric item?** Remain in Community tier. The rubric is the actionable feedback for what is needed to earn the Vouched badge. Ask for help if you are unsure how to proceed.
- **Can the rubric change?** Yes, post-v1.0. Changes will be proposed and debated in the community. Current rubric is frozen for v1.x.

---

*Vouched-badge rubric extracted from the vouchfx project plan and the architecture blueprint (docs/01 § 13). Authoritative source: engine [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md).*
