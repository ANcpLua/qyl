namespace Qyl.Contracts.Observability;

/// <summary>
///     Lifetime for <see cref="QylServiceAttribute" />. Mirrors
///     <c>Microsoft.Extensions.DependencyInjection.ServiceLifetime</c> in a BCL-only enum so
///     <c>qyl.contracts</c> stays free of DI-package references.
/// </summary>
public enum QylLifetime
{
    Singleton,
    Scoped,
    Transient
}

/// <summary>
///     Marks a class for automatic DI registration. The generator emits
///     <c>services.AddSingleton/Scoped/Transient&lt;T&gt;()</c> (or the <c>(IFoo, Foo)</c>
///     overload when <see cref="AsInterface" /> is set) into
///     <c>QylGeneratedRegistry.RegisterQylServices</c>, which the intercepted
///     <c>builder.Build()</c> calls automatically.
/// </summary>
/// <param name="Lifetime">The service lifetime.</param>
/// <param name="AsInterface">
///     Optional service type (interface or base class) to register against. When null the
///     concrete type is used as both service and implementation.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QylServiceAttribute(QylLifetime Lifetime, Type? AsInterface = null) : Attribute
{
    public QylLifetime Lifetime { get; } = Lifetime;
    public Type? AsInterface { get; } = AsInterface;
}
