using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Selects which semconv stability tiers a marker's generated surface includes.
/// <see cref="StableOnly"/> mirrors the contrib/Java/Python "stable package"
/// projection; <see cref="AllStabilities"/> mirrors the "incubating package"
/// projection (stable + development + deprecated + alpha + beta + release_candidate).
/// </summary>
/// <remarks>
/// Phase A surfaces this enum on every marker model and through the per-signal
/// loader/emitter signatures. Phase B (instrument-surface, activity-surface)
/// consumes <see cref="StabilityFiltering.IsIncluded"/> inside each emitter to
/// actually drop non-stable rows when <see cref="StableOnly"/> is selected. Until
/// Phase B lands, the filter is carried through unchanged and behavior matches
/// the prior single-marker generator (all stabilities emitted).
/// </remarks>
internal enum StabilityFilter
{
    /// <summary>Emit only rows whose stability is <see cref="StabilityModel.Stable"/>.</summary>
    StableOnly,

    /// <summary>Emit rows of every stability tier (stable + experimental).</summary>
    AllStabilities
}

internal static class StabilityFiltering
{
    /// <summary>
    /// Returns <c>true</c> when a registry row of the given <paramref name="stability"/>
    /// should be emitted under the requested <paramref name="filter"/>. Surface emitters
    /// call this once per row to gate emission.
    /// </summary>
    public static bool IsIncluded(StabilityModel stability, StabilityFilter filter) =>
        filter == StabilityFilter.AllStabilities || stability == StabilityModel.Stable;
}
