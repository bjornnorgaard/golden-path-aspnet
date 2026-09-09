using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators.OpenApi;

[Generator]
public sealed class OpenApiEndpointGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor DuplicateOperationId = Descriptor("GP2000", "OpenAPI operationId must be unique", "OpenAPI operationId '{0}' is declared more than once. Operation IDs must be globally unique.");
    private static readonly DiagnosticDescriptor MissingImplementation = Descriptor("GP2001", "OpenAPI endpoint is not implemented", "OpenAPI operation '{0}' requires an implementation of '{1}'. Create a class that implements this interface.");
    private static readonly DiagnosticDescriptor DuplicateImplementation = Descriptor("GP2002", "OpenAPI endpoint has multiple implementations", "OpenAPI interface '{0}' has multiple implementations: {1}. Register exactly one implementation.");
    private static readonly DiagnosticDescriptor HandlerThrows = new("GP2003", "Endpoint handlers must return documented errors", "Handler '{0}' throws an exception. Return a result declared by the OpenAPI responses instead, and update the specification before adding a new error scenario.", "GoldenPath.OpenApi", DiagnosticSeverity.Warning, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".openapi.yaml", StringComparison.OrdinalIgnoreCase) || file.Path.EndsWith(".openapi.yml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => new InputFile(file.Path, file.GetText(ct)))
            .Collect();

        var classes = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntax, _) => ClassInfo.Create((ClassDeclarationSyntax)syntax.Node))
            .Collect();

        context.RegisterSourceOutput(files.Combine(classes), static (output, input) => Generate(output, input.Left, input.Right));
    }

    private static void Generate(SourceProductionContext output, ImmutableArray<InputFile> files, ImmutableArray<ClassInfo> classes)
    {
        var documents = files.Select(static file => OpenApiDocument.Parse(file.Path, file.Text)).Where(static document => document is not null).Cast<OpenApiDocument>().ToArray();
        var operations = documents.SelectMany(static document => document.Operations).ToArray();

        foreach (var group in operations.GroupBy(static operation => operation.Id, StringComparer.Ordinal).Where(static group => group.Count() > 1))
        {
            foreach (var operation in group)
            {
                output.ReportDiagnostic(Diagnostic.Create(DuplicateOperationId, operation.Location, operation.Id));
            }
        }

        foreach (var document in documents)
        {
            output.AddSource($"OpenApi/{document.Name}/Contracts.g.cs", SourceText.From(EmitContracts(document), Encoding.UTF8));
            output.AddSource($"OpenApi/{document.Name}/Endpoints.g.cs", SourceText.From(EmitEndpoints(document, classes, output), Encoding.UTF8));
        }
    }

    private static string EmitContracts(OpenApiDocument document)
    {
        var sb = Header($"{document.RootNamespace}.{document.Name}.Contracts");
        foreach (var schema in document.Schemas)
        {
            if (schema.IsEnum)
            {
                sb.AppendLine($"[global::System.Text.Json.Serialization.JsonConverterAttribute(typeof(global::System.Text.Json.Serialization.JsonStringEnumConverter<{schema.Name}>))]");
                sb.AppendLine($"public enum {schema.Name}");
                sb.AppendLine("{");
                for (var index = 0; index < schema.EnumValues.Length; index++)
                {
                    if (index < schema.EnumDescriptions.Length)
                    {
                        sb.AppendLine($"    [global::System.ComponentModel.DescriptionAttribute(\"{schema.EnumDescriptions[index].Replace("\\\"", "\\\\\"")}\")]");
                    }

                    sb.AppendLine($"    {schema.EnumValues[index]}{(index == schema.EnumValues.Length - 1 ? string.Empty : ",")}");
                }

                sb.AppendLine("}");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine($"public class {schema.Name}{(schema.Reference is null ? string.Empty : " : " + schema.Reference)}");
            sb.AppendLine("{");
            foreach (var property in schema.Properties)
            {
                sb.AppendLine($"    public {property.CSharpType} {property.CSharpName} {{ get; init; }}{(property.IsRequired && property.IsReferenceType ? " = null!;" : string.Empty)}");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EmitEndpoints(OpenApiDocument document, ImmutableArray<ClassInfo> classes, SourceProductionContext output)
    {
        var endpointsNamespace = $"{document.RootNamespace}.{document.Name}.Endpoints";
        var contractsNamespace = $"{document.RootNamespace}.{document.Name}.Contracts";
        var sb = Header(endpointsNamespace);
        var implementations = new Dictionary<string, ClassInfo>(StringComparer.Ordinal);

        foreach (var requestSchema in document.Operations.Select(static operation => operation.RequestSchema).Distinct(StringComparer.Ordinal))
        {
            var schema = document.Schemas.FirstOrDefault(candidate => candidate.Name == requestSchema);
            if (schema is not null)
            {
                EmitValidator(sb, schema, contractsNamespace);
            }
        }

        foreach (var operation in document.Operations)
        {
            var interfaceName = "I" + ToPascalCase(operation.Id) + "Endpoint";
            var matches = classes.Where(type => type.BaseTypes.Any(baseType => baseType.Equals(interfaceName, StringComparison.Ordinal) || baseType.EndsWith("." + interfaceName, StringComparison.Ordinal))).ToArray();
            if (matches.Length == 0)
            {
                output.ReportDiagnostic(Diagnostic.Create(MissingImplementation, operation.Location, operation.Id, interfaceName));
            }
            else if (matches.Length > 1)
            {
                output.ReportDiagnostic(Diagnostic.Create(DuplicateImplementation, operation.Location, interfaceName, string.Join(", ", matches.Select(static match => match.FullName))));
            }
            else
            {
                implementations.Add(operation.Id, matches[0]);
                if (matches[0].Throws)
                {
                    output.ReportDiagnostic(Diagnostic.Create(HandlerThrows, matches[0].Location, matches[0].FullName));
                }
            }

            sb.AppendLine($"public interface {interfaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    global::System.Threading.Tasks.Task<{operation.ResultType(contractsNamespace)}> HandleAsync({operation.RequestType(contractsNamespace)} request, global::System.Threading.CancellationToken ct);");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("public static class GeneratedOpenApiServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedOpenApiEndpoints(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");
        foreach (var operation in document.Operations)
        {
            if (implementations.TryGetValue(operation.Id, out var implementation))
            {
                sb.AppendLine($"        services.AddScoped<{"I" + ToPascalCase(operation.Id) + "Endpoint"}, global::{implementation.FullName}>();");
            }

            sb.AppendLine($"        services.AddScoped<global::FluentValidation.IValidator<{operation.RequestType(contractsNamespace)}>, {operation.RequestSchema}Validator>();");
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class GeneratedOpenApiEndpointRouteBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapGeneratedOpenApiEndpoints(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)");
        sb.AppendLine("    {");
        foreach (var operation in document.Operations)
        {
            var interfaceName = "I" + ToPascalCase(operation.Id) + "Endpoint";
            sb.AppendLine($"        endpoints.Map{ToPascalCase(operation.Method)}(\"{operation.Path}\", static async ({operation.RequestType(contractsNamespace)} request, {interfaceName} handler, global::FluentValidation.IValidator<{operation.RequestType(contractsNamespace)}> validator, global::System.Threading.CancellationToken ct) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var validation = await validator.ValidateAsync(request, ct);");
            sb.AppendLine("            if (!validation.IsValid)");
            sb.AppendLine("            {");
            sb.AppendLine("                return global::Microsoft.AspNetCore.Http.TypedResults.BadRequest((global::System.Collections.Generic.IReadOnlyDictionary<string, string[]>)validation.ToDictionary());");
            sb.AppendLine("            }");
            sb.AppendLine("            return await handler.HandleAsync(request, ct);");
            sb.AppendLine("        })");
            sb.AppendLine($"            .WithName(\"{operation.Id}\")");
            foreach (var response in operation.Responses)
            {
                sb.AppendLine($"            .Produces<{response.Type(contractsNamespace)}>({response.StatusCode}, \"{response.ContentType}\")");
            }

            sb.AppendLine(";");
        }

        sb.AppendLine("        return endpoints;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitValidator(StringBuilder sb, OpenApiSchema schema, string contractsNamespace)
    {
        sb.AppendLine($"public sealed class {schema.Name}Validator : global::FluentValidation.AbstractValidator<global::{contractsNamespace}.{schema.Name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public {schema.Name}Validator()");
        sb.AppendLine("    {");
        foreach (var property in schema.Properties)
        {
            if (!property.IsRequired && property.Pattern is null && property.MinLength is null && property.Minimum is null && property.Maximum is null) continue;
            sb.Append($"        RuleFor(request => request.{property.CSharpName})");
            if (property.IsRequired && property.CSharpType == "string") sb.Append(".NotEmpty()");
            if (property.MinLength is { } minLength) sb.Append($".MinimumLength({minLength})");
            if (property.Pattern is { } pattern) sb.Append($".Matches(\"{Escape(pattern)}\")");
            if (property.Minimum is { } minimum) sb.Append($".GreaterThanOrEqualTo({minimum})");
            if (property.Maximum is { } maximum) sb.Append($".LessThanOrEqualTo({maximum})");
            if (property.IsNullable && (property.Minimum is not null || property.Maximum is not null)) sb.Append($".When(request => request.{property.CSharpName}.HasValue)");
            sb.AppendLine(";");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static StringBuilder Header(string @namespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using global::FluentValidation;");
        sb.AppendLine("using global::Microsoft.AspNetCore.Http.HttpResults;");
        sb.AppendLine();
        sb.AppendLine($"namespace {@namespace};");
        sb.AppendLine();
        return sb;
    }

    private static DiagnosticDescriptor Descriptor(string id, string title, string message) => new(id, title, message, "GoldenPath.OpenApi", DiagnosticSeverity.Error, true);
    private static string ToPascalCase(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class InputFile
    {
        public InputFile(string path, SourceText? text)
        {
            Path = path;
            Text = text;
        }

        public string Path { get; }
        public SourceText? Text { get; }
    }

    private sealed class ClassInfo
    {
        public string FullName { get; private set; } = null!;
        public ImmutableArray<string> BaseTypes { get; private set; }
        public bool Throws { get; private set; }
        public Location Location { get; private set; } = null!;

        public static ClassInfo Create(ClassDeclarationSyntax declaration)
        {
            var namespaces = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(static item => item.Name.ToString());
            var containingTypes = declaration.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(static item => item.Identifier.ValueText);
            return new ClassInfo
            {
                FullName = string.Join(".", namespaces.Concat(containingTypes).Concat(new[] { declaration.Identifier.ValueText })),
                BaseTypes = declaration.BaseList?.Types.Select(static item => item.Type.ToString()).ToImmutableArray() ?? ImmutableArray<string>.Empty,
                Throws = declaration.DescendantNodes().Any(static node => node is ThrowStatementSyntax or ThrowExpressionSyntax),
                Location = declaration.Identifier.GetLocation()
            };
        }
    }
}