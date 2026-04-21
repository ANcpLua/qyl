namespace Qyl.Contracts.Observability;

/// <summary>
///     Marks a class implementing <c>Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck</c>
///     for automatic registration. The generator emits
///     <c>services.AddHealthChecks().AddCheck&lt;T&gt;(Name, failureStatus: null, tags: Tags)</c>
///     into <c>QylGeneratedRegistry.RegisterQylHealthChecks</c>, which the intercepted
///     <c>builder.Build()</c> calls automatically.
/// </summary>
/// <param name="Name">Unique identifier for the check (appears in health reports).</param>
/// <param name="Tags">
///     Tags used by the <c>/alive</c> and <c>/health</c> probe predicates. Use
///     <c>QylEndpoints.LiveTag</c> / <c>QylEndpoints.ReadyTag</c> for the canonical probes.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QylHealthCheckAttribute(string Name, params string[] Tags) : Attribute
{
    public string Name { get; } = Name;
    public string[] Tags { get; } = Tags;
}
