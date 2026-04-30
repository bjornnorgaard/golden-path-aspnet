using Microsoft.Extensions.DependencyInjection;

namespace Platform.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ServiceAttribute(ServiceLifetime lifetime, bool asSelf = false) : Attribute
{
    public ServiceLifetime Lifetime { get; } = lifetime;
    public bool AsSelf { get; } = asSelf;
}