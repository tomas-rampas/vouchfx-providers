# Verified-tier providers

Verified-tier submissions live here, one folder per provider.

Each folder contains:

- The provider project (referencing `Platform.Sdk` 1.0.0).
- A `*.Tests` project referencing `Platform.Sdk.Testing` 1.0.0 and using
  `ProviderTestHarness.RunSingleStepAsync` as the conformance gate.

The conformance workflow runs `dotnet test` over every `*.Tests` project in this
directory before a PR is merged.

See `template/Community.Steps.Hello` for the copyable starter and
`template/Community.Steps.Hello.Tests` for the conformance test pattern to follow.
