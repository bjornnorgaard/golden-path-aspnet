namespace Platform.Annotations;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class StrongIdAttribute(Type? underlyingType = null) : Attribute
{
    public Type UnderlyingType { get; } = underlyingType ?? typeof(Guid);
}