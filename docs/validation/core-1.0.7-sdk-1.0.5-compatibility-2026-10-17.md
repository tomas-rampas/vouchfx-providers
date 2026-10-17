# Core 1.0.7 custom-host compatibility with SDK 1.0.5 — 2026-10-17

Measured against Core 1.0.7 source commit
[`20d837a2f735c1c785db6593b58ebe10b3dd9f05`](https://github.com/tomas-rampas/vouchfx/commit/20d837a2f735c1c785db6593b58ebe10b3dd9f05)
and its official CLI package. The Core source tree was
`0df0b96852f472ff0a80f5c3a88ded0f61215c81`.

Previously built Hello and JsonRpc provider assemblies targeting the published
`Vouchfx.Sdk` 1.0.5 were run unchanged by a custom `ScenarioRunner` host. The
host's measured build, runtime and driver checks each exited 0 in the .NET SDK
10.0.401 environment. Runtime results were two business assertions passed,
zero other verdicts and one loaded `Vouchfx.Sdk` 1.0.7.0 identity. Five official
Core DLL hashes matched between the installed CLI and package bytes; the two
SDK 1.0.5 provider references and their accepted binary SHA-256 values remained
unchanged.

This is custom-host ABI compatibility evidence only. It does not test external
provider loading by the stock CLI, or establish a new SDK 1.0.5 package release.
