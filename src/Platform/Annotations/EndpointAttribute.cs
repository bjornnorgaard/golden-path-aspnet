namespace Platform.Annotations;

public enum EndpointMethod
{
    Get = 0,
    Post = 1,
    Put = 2,
    Patch = 3,
    Delete = 4
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class EndpointAttribute(string route, EndpointMethod method) : Attribute
{
    public string Route { get; } = route;
    public EndpointMethod Method { get; } = method;
}