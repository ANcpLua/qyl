// Phase C-1 consumer fixture: exercises the most universally-portable surface
// in the Qyl.OpenTelemetry.SemanticConventions.SourceGeneration pack — the
// PR-A `[SemanticConventionAttributes(...)]` marker. The emitted output is
// pure `public const string Foo = "...";` plus optional `[Obsolete]` and
// nested static enum-value classes — all language features present in C# 1.0
// and every modern TFM. If this fixture fails to compile on any TFM, the
// failure is a real portability bug, not a "missing modern surface" gap.
//
// Phase A's stable/incubating marker split (in-progress on
// agent/generator-foundation-eng-stability) will add markers like
// SemanticConventionAttributesStable / -Incubating; once that lands, this
// fixture should grow a second marker covering the split. Surfaces with
// modern-only syntax (record struct payloads from SemanticConventionEvents,
// init-only properties) will need a polyfill story (IsExternalInit on ns2.0/
// net472) before they can be added to this matrix.

using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

namespace Qyl.SemConv.Smoke.Matrix;

[SemanticConventionAttributes("disk")]
internal static partial class DiskAttributes;

// Touch one emitted constant from a static field so the per-TFM compile is
// forced to bind to the generated symbol, not just emit it. Without a binding
// site, an unused generated partial could in principle be optimised away
// before the compiler reports a missing-symbol error.
internal static class Anchor
{
    public const string DiskIoDirection = DiskAttributes.AttributeDiskIoDirection;
}
