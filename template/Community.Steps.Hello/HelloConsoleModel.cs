// ─────────────────────────────────────────────────────────────────────────────
// COPYABLE TEMPLATE — Community provider starter (S12-F-01).
//
// This file is the model for the `hello.console` template step kind.
// When you copy this project to start your own provider:
//   1. Rename the namespace (Community.Steps.Hello → YourOrg.Steps.YourKind).
//   2. Rename this record and adjust the fields to match your step's YAML shape.
//   3. Update HelloConsoleProvider.cs to match.
//
// §13 INVARIANT: models are RECORDS implementing IStepModel.
//               Never use Dictionary<string,object> for a step model.
// ─────────────────────────────────────────────────────────────────────────────
using Platform.Sdk;

namespace Community.Steps.Hello;

/// <summary>
/// Strongly-typed model for the template <c>hello.console</c> step kind.
/// </summary>
/// <remarks>
/// This is the canonical shape for a provider model (§13): an immutable
/// <see langword="record"/> implementing <see cref="IStepModel"/>, with one
/// property per field the test author writes in the <c>.e2e.yaml</c> step.
/// Using a record (rather than a loosely-typed dictionary) gives the binder,
/// validator and compiler a compile-time-checked contract to work against.
/// </remarks>
/// <param name="Message">
/// The greeting the step emits at execution time.  Bound from the step's
/// <c>message</c> field; the validator rejects an empty value.
/// </param>
/// <param name="Expected">
/// The constant the step asserts the emitted <see cref="Message"/> equals.
/// Bound from the optional <c>expect</c> field; when omitted it defaults to
/// <see cref="Message"/> so a bare emit (no assertion) always passes.
/// </param>
public sealed record HelloConsoleModel(string Message, string Expected) : IStepModel;
