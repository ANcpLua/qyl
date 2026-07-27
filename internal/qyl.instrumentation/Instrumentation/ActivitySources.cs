
namespace Qyl.Instrumentation.Instrumentation;

public static class ActivitySources
{
    /// <summary>
    /// The only ActivitySource this project emits. Everything else a qyl process traces comes from
    /// the producer packages through <c>AddQyl()</c>, which owns that inventory — naming sources
    /// here as well is how the two lists drifted apart in the first place.
    /// </summary>
    public const string ErrorCapture = "Qyl.Instrumentation.ErrorCapture";
}
