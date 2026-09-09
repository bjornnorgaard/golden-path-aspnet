using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;

namespace Generators.GraphQl;

internal sealed class GraphQlDocument
{
    private static readonly Regex TypePattern = new("^type (?<name>Query|Mutation)\\s*\\{(?<fields>.*?)^\\}", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
    private static readonly Regex FieldPattern = new("^\\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*\\(\\s*input:\\s*(?<input>[A-Za-z_][A-Za-z0-9_]*)!?\\s*\\)\\s*:\\s*(?<result>[A-Za-z_][A-Za-z0-9_]*)!?\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    private GraphQlDocument(string name, ImmutableArray<GraphQlOperation> operations)
    {
        Name = name;
        Operations = operations;
    }

    public string Name { get; }
    public ImmutableArray<GraphQlOperation> Operations { get; }

    public static GraphQlDocument? Parse(string path, SourceText? text)
    {
        if (text is null) return null;
        var operations = TypePattern.Matches(text.ToString())
            .Cast<Match>()
            .SelectMany(type => FieldPattern.Matches(type.Groups["fields"].Value).Cast<Match>().Select(field => new GraphQlOperation(
                field.Groups["name"].Value,
                type.Groups["name"].Value,
                field.Groups["input"].Value,
                field.Groups["result"].Value)))
            .ToImmutableArray();
        var name = Path.GetFileNameWithoutExtension(path);
        return new GraphQlDocument(char.ToUpperInvariant(name[0]) + name.Substring(1), operations);
    }
}

internal sealed class GraphQlOperation
{
    public GraphQlOperation(string id, string operationType, string requestType, string responseType)
    {
        Id = id;
        OperationType = operationType;
        RequestType = requestType;
        ResponseType = responseType;
    }

    public string Id { get; }
    public string OperationType { get; }
    public string RequestType { get; }
    public string ResponseType { get; }
}