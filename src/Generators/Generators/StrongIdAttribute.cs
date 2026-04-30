using System;

namespace Generators;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class StrongIdAttribute(string underlying = "System.Guid") : Attribute
{
    public string Underlying { get; } = underlying;
}