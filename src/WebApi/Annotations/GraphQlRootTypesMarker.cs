using System.Collections.Concurrent;

namespace WebApi.Annotations;

/// <summary>
/// Registered exactly once in DI, shared by every *.graphql document's generated code. HotChocolate
/// allows only one document to call AddQueryType()/AddMutationType() - every document (including
/// that one) still contributes its own operations via AddTypeExtension - and only one document may
/// map the shared "/graphql" route and its playground. This marker's presence in DI signals "a
/// document already claimed the schema roots" during service registration; TryClaim additionally
/// lets generated code check "am I first" for a specific shared route at endpoint-mapping time,
/// regardless of which document's generated code runs first.
/// </summary>
public sealed class GraphQlRootTypesMarker
{
    private readonly ConcurrentDictionary<string, byte> _claims = new();

    public bool TryClaim(string key) => _claims.TryAdd(key, 0);
}
