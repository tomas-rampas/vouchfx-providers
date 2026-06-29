# Verified-Tier Provider Submission

**Provider Name:** [Your Provider Name]  
**Step Type:** [e.g. `db-assert.snowflake`]  
**Repository:** [Link to your provider repo]

## Submission Checklist

Please confirm that your provider meets all six Verified-tier rubric items:

### 1. Conformance Matrix: Integration Tests Pass
- [ ] Integration-test fixture passes locally: `dotnet test verified/<provider-id>/tests/ -c Release`
- [ ] I understand that CI will run the fixture on the official matrix (engine main + 2 preceding minors)
- [ ] All tests pass without failures, flakes, or skips

### 2. Documentation: README with Use Cases and Known Limitations
- [ ] README included in `verified/<provider-id>/README.md` or referenced in the repository
- [ ] README contains at least three realistic, worked use cases with YAML `.e2e.yaml` examples
- [ ] README includes a "Known Limitations" section documenting edge cases, unsupported configurations, or performance characteristics
- [ ] README includes installation and setup instructions

### 3. Security Sign-Off
- [ ] **Credential Handling:** Credentials are never hardcoded, are resolved via `Vars.Secrets` at execution time, are never logged, and are transmitted over TLS where applicable. I have documented this.
- [ ] **Dependency Vulnerabilities:** I have scanned transitive dependencies (`dotnet list package --outdated`) and confirmed zero high-severity CVEs. I have documented any vulnerable transitive dependencies and their mitigations.
- [ ] **TLS Defaults:** Connections to external services default to TLS. If unencrypted connections are supported, they are opt-in only and documented as insecure. I have documented this.
- [ ] **Telemetry:** My provider does not phone home or exfiltrate data beyond what is required to interact with the target service. I have confirmed this.
- [ ] **Package Signature:** My NuGet package is signed (or I have documented why it is not).

### 4. Licence and DCO Sign-Off
- [ ] My provider is licenced under Apache-2.0 (or a compatible licence)
- [ ] My provider repository includes a `LICENSE` file with the Apache-2.0 text
- [ ] My provider's `.csproj` declares the licence: `<License>Apache-2.0</License>` (optional but recommended)
- [ ] All commits in this PR are signed off with DCO: `git commit -s` or GitHub "Sign off" checkbox
- [ ] I have read the [Developer Certificate of Origin](https://developercertificate.org/)

### 5. MinEngineVersion Declaration
- [ ] My provider's `IStepProvider.Metadata` property declares a `MinEngineVersion`
- [ ] The version is compatible with the engine's current major version
- [ ] The version reflects the actual API surface my provider uses

### 6. CsxFragment Composition Review (§13.3.1)
- [ ] I have read the CsxFragment composition rules in the engine's architecture blueprint (§13.3.1)
- [ ] My provider's `IStepCompiler<TModel>.Emit()` method follows all five rules:
  - [ ] Uses only three fields: `RequiredUsings`, `RequiredHelpers`, `StatementBlock`
  - [ ] Does not use `using var` in the Roslyn script body (uses plain `var` + explicit `Dispose()` in `finally`)
  - [ ] Emits bodies as C# 11 double-dollar raw strings (`$$"""…"""`)
  - [ ] Sanitises step IDs before using them in variable names: `CsxFragment.SanitiseId(stepId)`
  - [ ] Passes cross-step state only through `Vars`
- [ ] A representative step's emitted C# code has been reviewed by me for §13.3.1 conformance
- [ ] I have documented this review (see "CSX Review Evidence" section below)

## Additional Information

### Provider Overview
Brief description of what your provider does and its key capabilities.

### Testing Instructions
Steps to run your integration-test fixture locally:

```bash
cd verified/<your-provider-id>/tests
dotnet test -c Release
```

### Security Review Evidence
Provide the following for security sign-off:

- Credential handling strategy: [e.g., "Credentials are read from `Vars.Secrets.Resolve(ref)` and passed to Npgsql; no secrets are logged."]
- Transitive dependency scan result: [e.g., "Ran `dotnet list package --outdated` — zero high-severity CVEs. Pinned version X of library Y."]
- TLS defaults: [e.g., "PostgreSQL connections default to SSL=Require; unsecured connections are not supported."]
- Telemetry statement: [e.g., "This provider makes no outbound connections beyond PostgreSQL; there is no telemetry or analytics."]
- NuGet signature: [e.g., "Package is signed with code-signing certificate from Acme Corp." or "Package is unsigned for the following reason: …"]

### CSX Review Evidence
Provide evidence that a representative step's emitted CSX follows §13.3.1:

Example:

```csharp
// Representative step: SnowflakeAssertCompiler for a SELECT query assertion
// The Emit method produces the following CsxFragment:

var fragment = new CsxFragment(
    RequiredUsings: new[] { "System", "System.Data", "Snowflake.Data.Client" },
    RequiredHelpers: new[] { 
        """
        static class DbAssertSnowflake_Helpers
        {
            public static async System.Threading.Tasks.Task<object?> ExecuteQueryAsync(Platform.Engine.Abstractions.SecretString connString, string query)
            {
                var conn = new SnowflakeDbConnection(connString.Unseal());
                try
                {
                    await conn.OpenAsync();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = query;
                    return await cmd.ExecuteScalarAsync();
                }
                finally { conn.Dispose(); }
            }
        }
        """
    },
    StatementBlock: $$"""
    {
        var __secret_{{safeId}} = Vars.Secrets.Resolve({{secretRefLiteral}});
        var __result_{{safeId}} = await DbAssertSnowflake_Helpers.ExecuteQueryAsync(__secret_{{safeId}}, {{queryLiteral}});
        // assertion logic with __result_{{safeId}}...
    }
    """
);

// Compliance notes:
// - Three fields only (RequiredUsings, RequiredHelpers, StatementBlock)
// - No `using var`; explicit `Dispose()` in finally
// - Double-dollar raw string; single braces are literal; {{safeId}} is interpolated
// - Step ID sanitised before use in variable names
// - Secrets never leak into plain strings; SecretString.Unseal() called only inside trusted helper
// - Cross-step state passes through Vars only
```

### Known Issues or Caveats
Any known limitations, edge cases, or areas where the provider deviates from standard patterns. (This is separate from the README's "Known Limitations" section and documents submission-specific notes.)

## Checklist for Merge

- [ ] CI conformance tests pass on all versions in the official matrix
- [ ] Security review approved by a maintainer
- [ ] CSX review approved by a maintainer
- [ ] No merge conflicts with the `main` branch
- [ ] All commits are signed off with DCO

---

## Important Notes

**Do not mark items as complete unless they are truly done.** Incomplete submissions will be returned for revision. If you need help or clarification on any rubric item, open an issue or ask in this PR.

**Failure mode:** If any item is marked as incomplete or if a check fails, the maintainers will request changes. Push fixes to your PR branch; CI will re-run automatically.

**Timeline:** The maintainers allocate one half-day per week for provider support. Your PR will be reviewed within that window.

---

**Thank you for contributing to vouchfx. The Verified tier exists because of submissions like yours.**
