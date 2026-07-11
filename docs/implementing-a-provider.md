# Implementing a Provider — Journey Overview

Welcome to the vouchfx provider authoring guide. This page is your entry point to the seven-stage provider authoring journey. Below you will find a high-level overview of what a provider is, what you can build, and links to each stage.

The full authoring journey is a **restructured** set of focused documentation pages (previously one large guide). Each stage covers a specific part of the process, with breadcrumbs and navigation links to move forward.

## What a Provider Is

A provider is a compile-time C# plugin that converts a YAML step into an executable C# code fragment. It is **not** dynamic runtime loading; it is **not** sandboxing; it is discovered reflectively at suite startup and integrated into the engine's standard compilation and execution pipeline.

A step in vouchfx has a **type** of the form `<family>.<provider>` — for example, `http.rest`, `db-assert.postgres`, `mq-publish.kafka`. The family names the *intent* (what you are testing: a database, a message queue, an HTTP endpoint); the provider names the *technology* (PostgreSQL, Kafka, REST).

Your provider is a C# class decorated with `[StepProvider]` that:

- **Declares its step kind.** `Kind { get; } = new StepKindId("family", "provider")`
- **Binds YAML** from a `.e2e.yaml` file into a strongly-typed C# model (record)
- **Validates** the model against provider-specific and project-level rules
- **Compiles** the model into a `CsxFragment` — a reusable C# code snippet
- **Is discovered reflectively** by the engine at startup — no manual registration is needed

The engine calls your provider at compile time (once per suite), assembles all providers' CSX fragments into a single Roslyn script, compiles it once, then executes it in an isolated, collectible `AssemblyLoadContext`. Your provider never touches the execution path — it only emits code.

Providers are **compile-time, source-level plugins**. There is no dynamic assembly loading, no sandbox, no inheritance hierarchy you must fit into. Your provider is one of many implementations of a frozen v1 contract; evolution is additive only, via new optional interfaces.

## Governance Tiers

**The two governance tiers** distinguish bundling; the optional **Vouched badge** offers platform-team endorsement:

- **Core** — platform-team authored, bundled with the engine, versioned with the engine
- **Community** — community-authored, independently versioned, listed in the community registry with no platform endorsement
- **Vouched badge** — optional, awarded post-listing by a maintainer after security review and rubric validation; does not move the provider to a new tier, remaining community-owned and community-hosted

See [`GOVERNANCE.md`](../GOVERNANCE.md) for full details.

## What You Can Build Self-Contained

You can author a provider **entirely** in your own repository without modifying the engine if your provider falls into one of these categories:

### Protocol Providers — Direct to System Under Test

Your provider talks directly to a service the test author supplies as an absolute URL or address, resolved at step-execution time. The engine does not manage the infrastructure dependency.

**Examples:**
- `http.rest` — sends HTTP requests to an absolute URL
- `rpc.json-rpc` (the first community provider) — sends JSON-RPC 2.0 requests over HTTP
- Any provider that connects to a system by hostname/port/URL with no Aspire dependency management

**What you need:**
- Define the step model (YAML shape)
- Implement the four provider interfaces (covered in Stage 3)
- Emit CSX that uses only System.* and user-supplied libraries
- Write conformance tests (Stage 5)

### Infrastructure Providers — Observing Engine-Managed Dependencies

Your provider observes a dependency type the engine **already manages** via Aspire. The engine's orchestration already knows how to start the container, health-gate it, and expose its connection details via `ScriptGlobalVariables`.

**Managed dependency types (the engine orchestration already supports these):**
- Relational databases: `postgres`, `mysql`, `sqlserver`
- Document databases: `mongodb`, `dynamodb` (via `amazon/dynamodb-local`)
- Key-value & search: `redis`, `elasticsearch`
- Message brokers: `kafka`, `rabbitmq`, `nats`, `azureservicebus`
- Object storage: `minio` (S3-compatible)
- Mail sink: `mailpit` (SMTP test server)

**Examples:**
- `db-assert.postgres` — executes SQL assertions against PostgreSQL
- `mq-expect.kafka` — consumes and asserts on Kafka messages

### Providers Requiring New Infrastructure

Your provider needs Aspire to manage a **new container type** not in the list above (for example, a custom gRPC server, a proprietary database, a third-party cloud emulator). **This requires engine-side support.**

The orchestration layer's dependency registry is a fixed table; supporting a new dependency type means adding an entry there plus the matching validator/schema surface — it is not something a provider package can extend from outside the engine. That change therefore arrives via a contribution to, and release of, the [engine](https://github.com/tomas-rampas/vouchfx), not from your provider repository.

This is not a blocker — it is a deliberate separation of concerns. In the meantime, a protocol provider that talks to an already-running instance by URL needs no orchestration change at all.

## The Seven-Stage Journey

| Stage | Page | Topics |
|-------|------|--------|
| **1** | **[Template README](../template/Vouchfx.Community.Hello/README.md)** | Copy the scaffold provider; write documentation |
| **2** | **[Project Setup](provider-project-setup.md)** | Create your `.csproj`, name your namespace, understand the model shape |
| **3** | **[The Contract Surfaces](provider-contract.md)** | The four mandatory interfaces (`IStepProvider`, `IStepBinder`, `IStepValidator`, `IStepCompiler`) and three optional extension interfaces |
| **4** | **[CSX Composition](provider-csx-composition.md)** | The Roslyn composition rules, verdicts (Pass/Fail/EnvironmentError/Inconclusive), secrets, and capture |
| **5** | **[Testing](provider-testing.md)** | Conformance tests with `ProviderTestHarness`, custom harness pattern, unit tests, Docker integration tests |
| **6** | **[Publishing & Submission](provider-publishing.md)** | Community listing (external and hub-hosted paths), the Vouched badge rubric, and submission checklist |
| **7** | **[Community Registry](../registry/README.md)** & **[VOUCHED_CHECKLIST](../VOUCHED_CHECKLIST.md)** | How providers are indexed, discovered, and how to request Vouched endorsement |

## Before You Start

If you are new to provider authoring:

1. **See what a provider looks like:** The [`rpc.json-rpc`](../community/Vouchfx.Community.JsonRpc/README.md) provider is the hub-hosted reference implementation, and it doubles as the worked example the guide walks through.
2. **Understand how to consume providers:** See [Consuming a Provider](consuming-a-provider.md) to understand the compilation model and why there is no runtime loader.
3. **Read the engine's contract:** The engine's [`CONTRIBUTING.md`](https://github.com/tomas-rampas/vouchfx/blob/main/CONTRIBUTING.md) and [`docs/01`](https://github.com/tomas-rampas/vouchfx/blob/main/docs/01_Technical_Architecture_and_Engineering_Blueprint.md) § 13 are the authoritative statements of the frozen v1 SDK contract.

## Navigation

The stage pages use breadcrumb navigation. Each page shows which stage you are on and links forward to the next:

- You are here: **Overview & Journey Map**
- Next stage: [Template README](../template/Vouchfx.Community.Hello/README.md)

