# `hello.console` — copyable provider template

> **Replace this whole file.** It ships only so a copy of this template packs and
> passes the hub's pack gate on day one — `PackageReadmeFile=README.md` in
> [`Vouchfx.Community.Hello.csproj`](Vouchfx.Community.Hello.csproj) requires a
> `README.md` to exist right here, at the package root, so `dotnet pack` (and the
> CI pack gate that validates every `community/` submission) never fails on a
> missing readme. Every section below is a placeholder — swap the title, the
> description, and both example sections for your own provider before you submit.

`hello.console` is the smallest meaningful step kind: it emits a message and
asserts it equals a constant. It has no infrastructure dependency, which is why
[`Vouchfx.Community.Hello.Tests`](../Vouchfx.Community.Hello.Tests) runs the
conformance test end to end without Docker — copy this project to bootstrap your
own provider; see [`CONTRIBUTING.md`](../../CONTRIBUTING.md) for the full walk-through.

## Use cases

The [Vouched badge rubric](../../VOUCHED_CHECKLIST.md) (item 2, "Documentation")
requires **at least three** realistic, worked use cases before your provider can
be endorsed — each with a short scenario description, a `.e2e.yaml` step snippet,
and the expected outcome. This template ships only one, as a shape to copy:

### 1. Happy path

```yaml
steps:
  - id: greet
    type: hello.console
    message: "hello, world"
    expect: "hello, world"
```

Expected outcome: the step **passes**, because the emitted message equals `expect`.

<!-- Add at least two more use cases here before requesting the Vouched badge —
     e.g. a mismatch case (Fail), and a case exercising any optional field your
     real provider adds beyond `message`/`expect`. -->

## Known limitations

The Vouched rubric's "known limitations" section documents the boundaries of
what your provider does and does not do — not a bug list. Replace these with
your own:

- `hello.console` has no infrastructure dependency and no `IResourceContributor`
  — there is nothing for the engine to provision or wait for.
- The assertion is an ordinal string equality only; there is no fuzzy or
  pattern-based matching.
- `expect` defaults to `message` when omitted, so a bare emit with no `expect`
  field always passes — this is deliberate template behaviour, not a defect.

## Replacing this template

1. Rename the namespace, step kind, model, and helper-class prefix — see the
   header comment in [`HelloConsoleProvider.cs`](HelloConsoleProvider.cs).
2. Rewrite every section above for your own provider.
3. Replace `Description`, `Authors`, and `PackageTags` in
   [`Vouchfx.Community.Hello.csproj`](Vouchfx.Community.Hello.csproj) — the hub's
   pack gate validates all three (plus this readme) once your copy lives under
   `community/`.
