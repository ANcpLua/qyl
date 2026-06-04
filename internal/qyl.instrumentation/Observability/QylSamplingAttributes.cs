namespace Qyl.Instrumentation;

/// <summary>
/// Marks a method, type, or assembly whose database call sites must <b>not</b> be traced.
/// The auto-instrumentation source generator resolves this at <b>compile time</b> and emits a
/// pass-through interceptor (the raw ADO.NET call — no <see cref="System.Diagnostics.Activity"/>,
/// no sampler) for every matching call site. The sampling decision therefore costs zero runtime
/// instructions: it was made by Roslyn. This suppresses the child span regardless of the ambient
/// trace's sampling decision, which is the correct tool for silencing a known-noisy call without
/// breaking the surrounding (still head-sampled) trace.
/// Precedence is method &gt; containing type &gt; assembly.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly,
    Inherited = false)]
public sealed class QylNoTraceAttribute : Attribute;

/// <summary>
/// Sets a compile-time sampling ratio in <c>[0, 1]</c> for the database call sites in the annotated
/// scope (method, type, or assembly; method &gt; type &gt; assembly precedence).
/// <list type="bullet">
/// <item><c>0</c> — never traced; identical to <see cref="QylNoTraceAttribute"/> (compile-time drop).</item>
/// <item><c>1</c> — always offered to the runtime <c>QylAotSampler</c> (default behaviour).</item>
/// <item><c>0 &lt; r &lt; 1</c> — the generated interceptor applies a deterministic, allocation-free
/// trace-id gate <b>before</b> any <see cref="System.Diagnostics.Activity"/> is created, keyed off the
/// ambient trace so a whole trace is decided consistently.</item>
/// </list>
/// </summary>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly,
    Inherited = false)]
public sealed class QylSampleAttribute(double ratio) : Attribute
{
    /// <summary>The sampling ratio in <c>[0, 1]</c>.</summary>
    public double Ratio { get; } = ratio;
}
