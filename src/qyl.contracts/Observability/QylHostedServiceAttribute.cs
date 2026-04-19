namespace Qyl.Contracts.Observability;

/// <summary>
///     Marks a class implementing <c>Microsoft.Extensions.Hosting.IHostedService</c> for
///     automatic DI registration. The qyl.instrumentation.generators source generator emits
///     <c>services.AddHostedService&lt;T&gt;()</c> into <c>QylGeneratedRegistry.RegisterAll()</c>
///     for every tagged class — so consumers don't hand-wire hosted services anymore.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QylHostedServiceAttribute : Attribute;
