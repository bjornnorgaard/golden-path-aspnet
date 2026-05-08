namespace Platform.Annotations;

[AttributeUsage(AttributeTargets.Field)]
public sealed class TagKeyAttribute : Attribute
{
    public TagKeyAttribute(Type type, Type? strongIdType = null)
    {
        Type = type;
        StrongIdType = strongIdType;
    }

    public Type Type { get; }
    public Type? StrongIdType { get; }
}