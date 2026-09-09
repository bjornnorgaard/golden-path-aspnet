using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Generators.OpenApi;

internal sealed class OpenApiDocument
{
    private static readonly Regex PathPattern = new("^ {2}(?<path>/[^:]+):\\s*$", RegexOptions.Compiled);
    private static readonly Regex MethodPattern = new("^ {4}(?<method>get|post|put|patch|delete):\\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OperationIdPattern = new("^ {6}operationId:\\s*(?<id>[A-Za-z_][A-Za-z0-9_]*)\\s*$", RegexOptions.Compiled);
    private static readonly Regex ResponsePattern = new("^ {8}'?(?<status>[1-5][0-9]{2})'?:\\s*$", RegexOptions.Compiled);
    private static readonly Regex SchemaPattern = new("^ {4}(?<name>[A-Za-z_][A-Za-z0-9_]*):\\s*$", RegexOptions.Compiled);
    private static readonly Regex PropertyPattern = new("^ {8}(?<name>[A-Za-z_][A-Za-z0-9_]*):(?:\\s*(?<inline>\\{.*\\}))?\\s*$", RegexOptions.Compiled);
    private static readonly Regex ReferencePattern = new("\\$ref:\\s*['\"]?#/components/schemas/(?<name>[A-Za-z_][A-Za-z0-9_]*)['\"]?", RegexOptions.Compiled);

    private OpenApiDocument(string name, string rootNamespace, ImmutableArray<OpenApiOperation> operations, ImmutableArray<OpenApiSchema> schemas)
    {
        Name = name;
        RootNamespace = rootNamespace;
        Operations = operations;
        Schemas = schemas;
    }

    public string Name { get; }
    public string RootNamespace { get; }
    public ImmutableArray<OpenApiOperation> Operations { get; }
    public ImmutableArray<OpenApiSchema> Schemas { get; }

    public static OpenApiDocument? Parse(string path, SourceText? text)
    {
        if (text is null)
        {
            return null;
        }

        var lines = text.Lines.Select(static line => line.ToString()).ToArray();
        var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
        name = char.ToUpperInvariant(name[0]) + name.Substring(1);
        return new OpenApiDocument(name, "WebApi", ParseOperations(path, text, lines), ParseSchemas(lines));
    }

    private static ImmutableArray<OpenApiOperation> ParseOperations(string path, SourceText text, string[] lines)
    {
        var operations = ImmutableArray.CreateBuilder<OpenApiOperation>();
        for (var index = 0; index < lines.Length; index++)
        {
            var pathMatch = PathPattern.Match(lines[index]);
            if (!pathMatch.Success) continue;
            var route = pathMatch.Groups["path"].Value;
            var pathEnd = FindBlockEnd(lines, index + 1, 2);
            for (var cursor = index + 1; cursor < pathEnd; cursor++)
            {
                var methodMatch = MethodPattern.Match(lines[cursor]);
                if (!methodMatch.Success) continue;
                var operationEnd = FindBlockEnd(lines, cursor + 1, 4);
                var idLine = Enumerable.Range(cursor + 1, operationEnd - cursor - 1).FirstOrDefault(line => OperationIdPattern.IsMatch(lines[line]));
                var idMatch = idLine > 0 ? OperationIdPattern.Match(lines[idLine]) : Match.Empty;
                if (!idMatch.Success) continue;
                var responsesIndex = FindLine(lines, cursor + 1, operationEnd, "      responses:");
                var requestEnd = responsesIndex >= 0 ? responsesIndex : operationEnd;
                var request = FindReference(lines, cursor + 1, requestEnd) ?? "object";
                var responses = ParseResponses(lines, responsesIndex, operationEnd);
                var span = new TextSpan(text.Lines[idLine].Start + idMatch.Groups["id"].Index, idMatch.Groups["id"].Length);
                operations.Add(new OpenApiOperation(idMatch.Groups["id"].Value, route, methodMatch.Groups["method"].Value.ToLowerInvariant(), request, responses, Location.Create(path, span, text.Lines.GetLinePositionSpan(span))));
                cursor = operationEnd - 1;
            }

            index = pathEnd - 1;
        }

        return operations.ToImmutable();
    }

    private static ImmutableArray<OpenApiResponse> ParseResponses(string[] lines, int start, int end)
    {
        var responses = ImmutableArray.CreateBuilder<OpenApiResponse>();
        if (start < 0) return responses.ToImmutable();
        for (var index = start + 1; index < end; index++)
        {
            var match = ResponsePattern.Match(lines[index]);
            if (!match.Success) continue;
            var responseEnd = FindBlockEnd(lines, index + 1, 8, end);
            var responseLines = lines.Skip(index + 1).Take(responseEnd - index - 1).ToArray();
            var reference = FindReference(responseLines, 0, responseLines.Length) ?? TypeFrom(responseLines);
            var contentType = responseLines.Any(static line => line.Trim() == "text/plain:") ? "text/plain" : "application/json";
            responses.Add(new OpenApiResponse(int.Parse(match.Groups["status"].Value, CultureInfo.InvariantCulture), reference, contentType));
            index = responseEnd - 1;
        }

        return responses.ToImmutable();
    }

    private static ImmutableArray<OpenApiSchema> ParseSchemas(string[] lines)
    {
        var schemasStart = FindLine(lines, 0, lines.Length, "  schemas:");
        if (schemasStart < 0) return ImmutableArray<OpenApiSchema>.Empty;
        var schemas = ImmutableArray.CreateBuilder<OpenApiSchema>();
        for (var index = schemasStart + 1; index < lines.Length; index++)
        {
            var schemaMatch = SchemaPattern.Match(lines[index]);
            if (!schemaMatch.Success) continue;
            var end = FindBlockEnd(lines, index + 1, 4);
            var required = ParseRequired(lines.Skip(index + 1).Take(end - index - 1));
            var enumValues = ParseInlineValues(lines.Skip(index + 1).Take(end - index - 1), "enum:");
            var enumDescriptions = ParseInlineValues(lines.Skip(index + 1).Take(end - index - 1), "x-enum-descriptions:");
            var properties = ImmutableArray.CreateBuilder<OpenApiProperty>();
            for (var cursor = index + 1; cursor < end; cursor++)
            {
                var propertyMatch = PropertyPattern.Match(lines[cursor]);
                if (!propertyMatch.Success) continue;
                var propertyEnd = FindBlockEnd(lines, cursor + 1, 8, end);
                var propertyLines = (propertyMatch.Groups["inline"].Success ? new[] { propertyMatch.Groups["inline"].Value } : Array.Empty<string>()).Concat(lines.Skip(cursor + 1).Take(propertyEnd - cursor - 1)).ToArray();
                var name = propertyMatch.Groups["name"].Value;
                properties.Add(new OpenApiProperty(
                    name,
                    ToPascalCase(name),
                    TypeFrom(propertyLines),
                    required.Contains(name),
                    ParseStringFacet(propertyLines, "pattern:"),
                    ParseIntegerFacet(propertyLines, "minLength:"),
                    ParseIntegerFacet(propertyLines, "minimum:"),
                    ParseIntegerFacet(propertyLines, "maximum:")));
                cursor = propertyEnd - 1;
            }

            var schemaReference = lines
                .Skip(index + 1)
                .Take(end - index - 1)
                .Where(static line => CountSpaces(line) == 6)
                .Select(line => ReferencePattern.Match(line))
                .Where(static match => match.Success)
                .Select(static match => match.Groups["name"].Value)
                .FirstOrDefault();
            schemas.Add(new OpenApiSchema(schemaMatch.Groups["name"].Value, properties.ToImmutable(), schemaReference, enumValues, enumDescriptions));
            index = end - 1;
        }

        return schemas.ToImmutable();
    }

    private static string TypeFrom(string[] lines)
    {
        var reference = FindReference(lines, 0, lines.Length);
        var typeLine = lines.FirstOrDefault(static line => line.TrimStart().StartsWith("type:", StringComparison.Ordinal));
        var shape = string.Join(" ", lines);
        var nullable = shape.Contains("null", StringComparison.Ordinal);
        string type;
        if (shape.Contains("type: object", StringComparison.Ordinal) && lines.Any(static line => line.TrimStart().StartsWith("additionalProperties:", StringComparison.Ordinal))) type = "global::System.Collections.Generic.IReadOnlyDictionary<string, string[]>";
        else if (shape.Contains("type: array", StringComparison.Ordinal)) type = "global::System.Collections.Generic.IReadOnlyList<" + (FindReference(lines, 0, lines.Length) ?? "object") + ">";
        else if (reference is not null) type = reference;
        else if (shape.Contains("format: uuid", StringComparison.Ordinal)) type = "global::System.Guid";
        else if (shape.Contains("format: date-time", StringComparison.Ordinal)) type = "global::System.DateTimeOffset";
        else if (shape.Contains("integer", StringComparison.Ordinal)) type = "int";
        else if (shape.Contains("boolean", StringComparison.Ordinal)) type = "bool";
        else type = "string";
        return nullable && type != "string" && !type.EndsWith("?", StringComparison.Ordinal) ? type + "?" : type == "string" && nullable ? "string?" : type;
    }

    private static HashSet<string> ParseRequired(IEnumerable<string> lines)
    {
        var line = lines.FirstOrDefault(static item => item.TrimStart().StartsWith("required:", StringComparison.Ordinal));
        if (line is null) return new HashSet<string>(StringComparer.Ordinal);
        var open = line.IndexOf('[');
        var close = line.IndexOf(']');
        return open < 0 || close < open ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(line.Substring(open + 1, close - open - 1).Split(',').Select(static item => item.Trim()), StringComparer.Ordinal);
    }

    private static ImmutableArray<string> ParseInlineValues(IEnumerable<string> lines, string key)
    {
        var line = lines.FirstOrDefault(item => item.TrimStart().StartsWith(key, StringComparison.Ordinal));
        if (line is null) return ImmutableArray<string>.Empty;
        var open = line.IndexOf('[');
        var close = line.LastIndexOf(']');
        if (open < 0 || close < open) return ImmutableArray<string>.Empty;
        return line.Substring(open + 1, close - open - 1)
            .Split(',')
            .Select(static item => item.Trim().Trim('\'', '\"'))
            .Where(static item => item.Length > 0)
            .ToImmutableArray();
    }

    private static string? ParseStringFacet(IEnumerable<string> lines, string key)
    {
        var line = lines.FirstOrDefault(item => item.TrimStart().StartsWith(key, StringComparison.Ordinal));
        return line is null ? null : line.Substring(line.IndexOf(':') + 1).Trim().Trim('\'', '\"');
    }

    private static int? ParseIntegerFacet(IEnumerable<string> lines, string key)
    {
        var value = ParseStringFacet(lines, key);
        return int.TryParse(value, out var result) ? result : null;
    }

    private static int FindBlockEnd(string[] lines, int start, int indentation, int maximum = -1)
    {
        var end = maximum < 0 ? lines.Length : maximum;
        for (var index = start; index < end; index++)
            if (lines[index].Length > 0 && CountSpaces(lines[index]) <= indentation)
                return index;
        return end;
    }

    private static int FindLine(string[] lines, int start, int end, string value)
    {
        for (var i = start; i < end; i++)
            if (lines[i] == value)
                return i;
        return -1;
    }

    private static string? FindReference(IEnumerable<string> lines, int start, int end) => lines.Skip(start).Take(end - start).Select(line => ReferencePattern.Match(line)).Where(static match => match.Success).Select(static match => match.Groups["name"].Value).FirstOrDefault();

    private static int CountSpaces(string value)
    {
        var count = 0;
        while (count < value.Length && value[count] == ' ') count++;
        return count;
    }

    private static string ToPascalCase(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);
}

internal sealed class OpenApiOperation
{
    public OpenApiOperation(string id, string path, string method, string requestSchema, ImmutableArray<OpenApiResponse> responses, Location location)
    {
        Id = id;
        Path = path;
        Method = method;
        RequestSchema = requestSchema;
        Responses = responses;
        Location = location;
    }

    public string Id { get; }
    public string Path { get; }
    public string Method { get; }
    public string RequestSchema { get; }
    public ImmutableArray<OpenApiResponse> Responses { get; }
    public Location Location { get; }
    public string RequestType(string contractsNamespace) => RequestSchema == "object" ? "object" : "global::" + contractsNamespace + "." + RequestSchema;
    public string ResultType(string contractsNamespace) => "global::Microsoft.AspNetCore.Http.HttpResults.Results<" + string.Join(", ", Responses.Select(response => response.ResultType(contractsNamespace))) + ">";
}

internal sealed class OpenApiResponse
{
    public OpenApiResponse(int statusCode, string schema, string contentType)
    {
        StatusCode = statusCode;
        Schema = schema;
        ContentType = contentType;
    }

    public int StatusCode { get; }
    public string Schema { get; }
    public string ContentType { get; }
    public string Type(string contractsNamespace) => Schema is "object" or "string" or "int" or "bool" ? Schema : Schema.StartsWith("global::", StringComparison.Ordinal) ? Schema : "global::" + contractsNamespace + "." + Schema;
    public string ResultType(string contractsNamespace) => StatusCode switch { 200 or 201 => "global::Microsoft.AspNetCore.Http.HttpResults.Ok<" + Type(contractsNamespace) + ">", 400 => "global::Microsoft.AspNetCore.Http.HttpResults.BadRequest<" + Type(contractsNamespace) + ">", 404 => "global::Microsoft.AspNetCore.Http.HttpResults.NotFound<" + Type(contractsNamespace) + ">", _ => "global::Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult" };
}

internal sealed class OpenApiSchema
{
    public OpenApiSchema(string name, ImmutableArray<OpenApiProperty> properties, string? reference, ImmutableArray<string> enumValues, ImmutableArray<string> enumDescriptions)
    {
        Name = name;
        Properties = properties;
        Reference = reference;
        EnumValues = enumValues;
        EnumDescriptions = enumDescriptions;
    }

    public string Name { get; }
    public ImmutableArray<OpenApiProperty> Properties { get; }
    public string? Reference { get; }
    public ImmutableArray<string> EnumValues { get; }
    public ImmutableArray<string> EnumDescriptions { get; }
    public bool IsEnum => !EnumValues.IsEmpty;
}

internal sealed class OpenApiProperty
{
    public OpenApiProperty(string name, string cSharpName, string cSharpType, bool isRequired, string? pattern, int? minLength, int? minimum, int? maximum)
    {
        Name = name;
        CSharpName = cSharpName;
        CSharpType = cSharpType;
        IsRequired = isRequired;
        Pattern = pattern;
        MinLength = minLength;
        Minimum = minimum;
        Maximum = maximum;
    }

    public string Name { get; }
    public string CSharpName { get; }
    public string CSharpType { get; }
    public bool IsRequired { get; }
    public string? Pattern { get; }
    public int? MinLength { get; }
    public int? Minimum { get; }
    public int? Maximum { get; }
    public bool IsReferenceType => CSharpType == "string" || CSharpType.StartsWith("global::System.Collections", StringComparison.Ordinal);
    public bool IsNullable => CSharpType.EndsWith("?", StringComparison.Ordinal);
}