using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators.GraphQl;

[Generator]
public sealed class GraphQlEndpointGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MissingResolver = new("GP3000", "GraphQL resolver is not implemented", "GraphQL operation '{0}' requires an implementation of '{1}'. Create a class that implements this interface.", "GoldenPath.GraphQl", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".graphql", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => new InputFile(file.Path, file.GetText(ct)))
            .Collect();
        var classes = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntax, _) => ClassInfo.Create((ClassDeclarationSyntax)syntax.Node))
            .Collect();

        var assemblyName = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? "Application");
        context.RegisterSourceOutput(files.Combine(classes).Combine(assemblyName), static (output, input) => Generate(output, input.Left.Left, input.Left.Right, input.Right));
    }

    private static void Generate(SourceProductionContext output, ImmutableArray<InputFile> files, ImmutableArray<ClassInfo> classes, string rootNamespace)
    {
        foreach (var file in files)
        {
            var document = GraphQlDocument.Parse(file.Path, file.Text);
            if (document is null) continue;
            output.AddSource($"GraphQl/{document.Name}/Endpoints.g.cs", SourceText.From(Emit(document, rootNamespace, classes, output), Encoding.UTF8));
            output.AddSource($"GraphQl/{document.Name}/TransportExtensions.g.cs", SourceText.From(EmitTransportExtensions(document, rootNamespace), Encoding.UTF8));
        }
    }

    private static string Emit(GraphQlDocument document, string rootNamespace, ImmutableArray<ClassInfo> classes, SourceProductionContext output)
    {
        var contractsNamespace = $"{rootNamespace}.{document.Name}.Contracts";
        var classPrefix = $"Generated{document.Name}GraphQl";
        var queryOperations = document.Operations.Where(static operation => operation.OperationType == "Query").ToArray();
        var mutationOperations = document.Operations.Where(static operation => operation.OperationType == "Mutation").ToArray();
        var sb = Header($"{rootNamespace}.{document.Name}.GraphQl");

        foreach (var operation in document.Operations)
        {
            var interfaceName = ResolverInterfaceName(operation);
            sb.AppendLine($"public interface {interfaceName}");
            sb.AppendLine("{");
            if (operation.OperationType == "Query")
            {
                sb.AppendLine($"    global::System.Threading.Tasks.Task<global::{contractsNamespace}.{operation.ResponseType}> ResolveAsync(global::{contractsNamespace}.{operation.RequestType} input, global::HotChocolate.Resolvers.IResolverContext resolverContext, global::System.Threading.CancellationToken ct);");
            }
            else
            {
                sb.AppendLine($"    global::System.Threading.Tasks.Task<global::{contractsNamespace}.{operation.ResponseType}> ResolveAsync(global::{contractsNamespace}.{operation.RequestType} input, global::System.Threading.CancellationToken ct);");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("public static class GeneratedGraphQlServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedGraphQlEndpoints(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");
        foreach (var operation in document.Operations)
        {
            var interfaceName = ResolverInterfaceName(operation);
            var matches = classes.Where(type => type.BaseTypes.Any(baseType => baseType.Equals(interfaceName, StringComparison.Ordinal) || baseType.EndsWith("." + interfaceName, StringComparison.Ordinal))).ToArray();
            if (matches.Length != 1)
            {
                output.ReportDiagnostic(Diagnostic.Create(MissingResolver, Location.None, operation.Id, interfaceName));
                continue;
            }

            sb.AppendLine($"        services.AddScoped<{interfaceName}, global::{matches[0].FullName}>();");
        }

        sb.AppendLine("        services.AddGraphQLServer()");
        sb.AppendLine($"            .AddQueryType<{classPrefix}Query>()");
        sb.AppendLine($"            .AddMutationType<{classPrefix}Mutation>();");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class GeneratedGraphQlEndpointRouteBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapGeneratedGraphQlEndpoints(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)");
        sb.AppendLine("    {");
        sb.AppendLine("        endpoints.MapGraphQL(\"/graphql\");");
        sb.AppendLine("        return endpoints;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapGeneratedGraphQlPlayground(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)");
        sb.AppendLine("    {");
        sb.AppendLine("        endpoints.MapNitroApp(\"/graphql/playground\", \"/graphql\");");
        sb.AppendLine("        return endpoints;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        EmitTelemetry(sb, classPrefix);
        EmitOperationType(sb, $"{classPrefix}Query", $"{classPrefix}Telemetry", queryOperations, contractsNamespace);
        EmitOperationType(sb, $"{classPrefix}Mutation", $"{classPrefix}Telemetry", mutationOperations, contractsNamespace);
        return sb.ToString();
    }

    private static void EmitTelemetry(StringBuilder sb, string classPrefix)
    {
        sb.AppendLine($"internal static class {classPrefix}Telemetry");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly global::System.Diagnostics.ActivitySource ActivitySource = new(\"GoldenPath.GraphQL\");");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void EmitOperationType(StringBuilder sb, string typeName, string telemetryTypeName, GraphQlOperation[] operations, string contractsNamespace)
    {
        sb.AppendLine($"public sealed class {typeName}");
        sb.AppendLine("{");
        foreach (var operation in operations)
        {
            var interfaceName = ResolverInterfaceName(operation);
            var isQuery = operation.OperationType == "Query";
            sb.AppendLine($"    [global::HotChocolate.GraphQLNameAttribute(\"{operation.Id}\")]");
            if (isQuery)
            {
                sb.AppendLine($"    public async global::System.Threading.Tasks.Task<global::{contractsNamespace}.{operation.ResponseType}> {ToPascalCase(operation.Id)}Async(global::{contractsNamespace}.{operation.RequestType} input, [global::HotChocolate.ServiceAttribute] {interfaceName} resolver, [global::HotChocolate.ServiceAttribute] global::FluentValidation.IValidator<global::{contractsNamespace}.{operation.RequestType}> validator, global::HotChocolate.Resolvers.IResolverContext resolverContext, global::System.Threading.CancellationToken ct)");
            }
            else
            {
                sb.AppendLine($"    public async global::System.Threading.Tasks.Task<global::{contractsNamespace}.{operation.ResponseType}> {ToPascalCase(operation.Id)}Async(global::{contractsNamespace}.{operation.RequestType} input, [global::HotChocolate.ServiceAttribute] {interfaceName} resolver, [global::HotChocolate.ServiceAttribute] global::FluentValidation.IValidator<global::{contractsNamespace}.{operation.RequestType}> validator, global::System.Threading.CancellationToken ct)");
            }

            sb.AppendLine("    {");
            sb.AppendLine($"        const string operationName = \"graphql.{operation.OperationType.ToLowerInvariant()}.{operation.Id}\";");
            sb.AppendLine("        global::System.Diagnostics.Activity.Current?.SetTag(\"graphql.operation.name\", operationName);");
            sb.AppendLine($"        using var activity = {telemetryTypeName}.ActivitySource.StartActivity(operationName, global::System.Diagnostics.ActivityKind.Internal);");
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine("            var validation = await validator.ValidateAsync(input, ct);");
            sb.AppendLine("            if (!validation.IsValid)");
            sb.AppendLine("            {");
            sb.AppendLine("                throw new global::HotChocolate.GraphQLException(string.Join(\"; \", global::System.Linq.Enumerable.Select(validation.Errors, static failure => failure.PropertyName + \": \" + failure.ErrorMessage))); ");
            sb.AppendLine("            }");
            if (isQuery)
            {
                sb.AppendLine("            return await resolver.ResolveAsync(input, resolverContext, ct);");
            }
            else
            {
                sb.AppendLine("            return await resolver.ResolveAsync(input, ct);");
            }

            sb.AppendLine("        }");
            sb.AppendLine("        catch (global::System.Exception ex)");
            sb.AppendLine("        {");
            sb.AppendLine("            activity?.AddException(ex);");
            sb.AppendLine("            activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, \"GraphQL operation failed\");");
            sb.AppendLine("            activity?.SetTag(\"error.type\", ex.GetType().FullName);");
            sb.AppendLine("            throw;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string EmitTransportExtensions(GraphQlDocument document, string rootNamespace)
    {
        var sb = Header($"{rootNamespace}.{document.Name}");
        sb.AppendLine("public static class GeneratedTransportLayerExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Microsoft.AspNetCore.Builder.WebApplicationBuilder AddGeneratedTransportLayers(this global::Microsoft.AspNetCore.Builder.WebApplicationBuilder builder)");
        sb.AppendLine("    {");
        sb.AppendLine($"        global::{rootNamespace}.{document.Name}.Endpoints.GeneratedOpenApiServiceCollectionExtensions.AddGeneratedOpenApiEndpoints(builder.Services);");
        sb.AppendLine($"        global::{rootNamespace}.{document.Name}.GraphQl.GeneratedGraphQlServiceCollectionExtensions.AddGeneratedGraphQlEndpoints(builder.Services);");
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapGeneratedTransportLayers(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)");
        sb.AppendLine("    {");
        sb.AppendLine($"        global::{rootNamespace}.{document.Name}.Endpoints.GeneratedOpenApiEndpointRouteBuilderExtensions.MapGeneratedOpenApiEndpoints(endpoints);");
        sb.AppendLine($"        global::{rootNamespace}.{document.Name}.GraphQl.GeneratedGraphQlEndpointRouteBuilderExtensions.MapGeneratedGraphQlEndpoints(endpoints);");
        sb.AppendLine("        return endpoints;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static StringBuilder Header(string @namespace) => new StringBuilder("// <auto-generated/>\n#nullable enable\nusing global::HotChocolate.AspNetCore.Extensions;\n\nnamespace ").Append(@namespace).AppendLine(";").AppendLine();
    private static string ResolverInterfaceName(GraphQlOperation operation) => "I" + ToPascalCase(operation.Id) + "Resolver";
    private static string ToPascalCase(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);

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

        public static ClassInfo Create(ClassDeclarationSyntax declaration)
        {
            var namespaces = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(static item => item.Name.ToString());
            var containingTypes = declaration.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(static item => item.Identifier.ValueText);
            return new ClassInfo
            {
                FullName = string.Join(".", namespaces.Concat(containingTypes).Concat(new[] { declaration.Identifier.ValueText })),
                BaseTypes = declaration.BaseList?.Types.Select(static item => item.Type.ToString()).ToImmutableArray() ?? ImmutableArray<string>.Empty
            };
        }
    }
}