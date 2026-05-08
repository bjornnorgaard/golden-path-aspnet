using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators;

[Generator]
public sealed class EndpointRegistrationGenerator : IIncrementalGenerator
{
    private const string AttrFqn = "Platform.Annotations.EndpointAttribute";
    private const string EndpointMethodEnumFqn = "Platform.Annotations.EndpointMethod";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
                AttrFqn,
                predicate: static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (syntaxContext, _) =>
                {
                    var classDecl = (ClassDeclarationSyntax)syntaxContext.TargetNode;
                    var symbol = syntaxContext.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                    if (symbol == null) return null;

                    var attr = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AttrFqn);
                    if (attr == null) return null;

                    // route (string)
                    if (attr.ConstructorArguments.Length < 2) return null;
                    if (attr.ConstructorArguments[0].Value is not string route) return null;

                    // method (enum -> int)
                    var methodValue = 0;
                    var methodArg = attr.ConstructorArguments[1];
                    if (methodArg.Type?.ToDisplayString() != EndpointMethodEnumFqn || methodArg.Value is not int mv)
                    {
                        return null;
                    }

                    methodValue = mv;

                    // Endpoint name now defaults to the attributed type name.
                    var endpointName = symbol.Name;

                    // Feature endpoint: IFeature<TRequestBody, TResponseBody, TCommand, TResult, THandler>
                    var featureContract = symbol.AllInterfaces.FirstOrDefault(IsFeatureContract);
                    if (featureContract == null)
                    {
                        // Fallback: endpoint class with public static Handle(...) method
                        var handle = symbol.GetMembers("Handle")
                            .OfType<IMethodSymbol>()
                            .FirstOrDefault(m => m is { IsStatic: true, DeclaredAccessibility: Accessibility.Public });
                        if (handle == null) return null;

                        return EndpointModel.ForDirectEndpoint(
                            endpointType: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            route: route,
                            method: methodValue,
                            name: endpointName
                        );
                    }

                    if (featureContract.TypeArguments.Length != 5) return null;
                    var requestBodyType = featureContract.TypeArguments[0];
                    var responseBodyType = featureContract.TypeArguments[1];
                    var commandType = featureContract.TypeArguments[2];
                    var resultType = featureContract.TypeArguments[3];
                    var handlerType = featureContract.TypeArguments[4] as INamedTypeSymbol;
                    if (handlerType == null) return null;

                    // require Handler.Handle(TCommand, CancellationToken)
                    var handleMethod = handlerType.GetMembers("Handle")
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m =>
                            m is { IsStatic: false, DeclaredAccessibility: Accessibility.Public } &&
                            m.Parameters.Length == 2 &&
                            SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, commandType) &&
                            m.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                            "global::System.Threading.CancellationToken");
                    if (handleMethod == null) return null;

                    var handlerDisplay = handlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var requestBodyDisplay = requestBodyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var responseBodyDisplay = responseBodyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var commandDisplay = commandType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var resultDisplay = resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    return EndpointModel.ForFeature(
                        featureType: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        featureName: endpointName,
                        handlerType: handlerDisplay,
                        requestBodyType: requestBodyDisplay,
                        responseBodyType: responseBodyDisplay,
                        commandType: commandDisplay,
                        resultType: resultDisplay,
                        route: route,
                        method: methodValue,
                        name: endpointName
                    );
                })
            .Where(static m => m != null);

        var collected = candidates.Collect();

        context.RegisterSourceOutput(collected, (ctx, batch) =>
        {
            var models = batch
                .Where(static m => m != null)
                .Select(static m => m!)
                .OrderBy(static m => m.Route, StringComparer.Ordinal)
                .ThenBy(static m => m.FeatureType, StringComparer.Ordinal)
                .ToArray();

            ctx.AddSource("EndpointRegistration.g.cs", SourceText.From(Emit(models), Encoding.UTF8));
        });
    }

    private static string Emit(IReadOnlyList<EndpointModel> models)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using global::Microsoft.AspNetCore.Http;");
        sb.AppendLine("using global::Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using global::System.Linq;");
        sb.AppendLine("namespace Microsoft.AspNetCore.Builder;");
        sb.AppendLine();
        sb.AppendLine("public static partial class GeneratedEndpointRouteBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapGeneratedEndpoints(");
        sb.AppendLine("        this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)");
        sb.AppendLine("    {");

        if (models.Count > 0)
        {
            sb.AppendLine("        var sp = app.ServiceProvider;");
        }

        foreach (var ep in models)
        {
            var target = ep.IsFeature
                ? $"__Handle_{Sanitize(ep.FeatureType!)}"
                : $"{ep.EndpointType!}.Handle";

            var mapCall = ep.Method switch
            {
                0 => $"        app.MapGet(@\"{Escape(ep.Route)}\", {target})",
                1 => $"        app.MapPost(@\"{Escape(ep.Route)}\", {target})",
                2 => $"        app.MapPut(@\"{Escape(ep.Route)}\", {target})",
                3 => $"        app.MapPatch(@\"{Escape(ep.Route)}\", {target})",
                4 => $"        app.MapDelete(@\"{Escape(ep.Route)}\", {target})",
                _ => $"        app.MapMethods(@\"{Escape(ep.Route)}\", new[] {{ \"POST\" }}, {target})"
            };

            mapCall += $".WithName(@\"{Escape(ep.Name)}\")";

            sb.AppendLine(mapCall + ";");
        }

        sb.AppendLine("        return app;");
        sb.AppendLine("    }");

        // Generated handlers (validation + call into feature handler)
        foreach (var ep in models.Where(m => m.IsFeature))
        {
            var handlerMethodName = $"__Handle_{Sanitize(ep.FeatureType!)}";

            sb.AppendLine();
            sb.AppendLine($"    private static async global::System.Threading.Tasks.Task<IResult> {handlerMethodName}(");
            sb.AppendLine($"        [global::Microsoft.AspNetCore.Mvc.FromBody] {ep.RequestBodyType} req,");
            sb.AppendLine($"        {ep.HandlerType} handler,");
            sb.AppendLine("        global::System.IServiceProvider sp,");
            sb.AppendLine("        global::System.Threading.CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var validator = sp.GetService<global::FluentValidation.IValidator<{ep.RequestBodyType}>>();");
            sb.AppendLine("        if (validator != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            var validation = await validator.ValidateAsync(req, ct).ConfigureAwait(false);");
            sb.AppendLine("            if (!validation.IsValid)");
            sb.AppendLine("            {");
            sb.AppendLine("                var errors = validation.Errors");
            sb.AppendLine("                    .GroupBy(e => e.PropertyName)");
            sb.AppendLine("                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());");
            sb.AppendLine();
            sb.AppendLine("                return TypedResults.BadRequest(new { errors });");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        var cmd = {ep.FeatureType}.MapToCommand(req);");
            sb.AppendLine("        var outcome = await handler.Handle(cmd, ct).ConfigureAwait(false);");
            sb.AppendLine("        switch (outcome.Status)");
            sb.AppendLine("        {");
            sb.AppendLine("            case global::System.Net.HttpStatusCode.OK:");
            sb.AppendLine("            {");
            sb.AppendLine("                // Contract: Ok implies Value is present.");
            sb.AppendLine($"                var body = {ep.FeatureType}.MapToResponseBody(outcome.Value!);");
            sb.AppendLine("                return TypedResults.Ok(body);");
            sb.AppendLine("            }");
            sb.AppendLine("            case global::System.Net.HttpStatusCode.BadRequest:");
            sb.AppendLine("                return TypedResults.BadRequest(outcome.Failure);");
            sb.AppendLine("            case global::System.Net.HttpStatusCode.NotFound:");
            sb.AppendLine("                return TypedResults.NotFound(outcome.Failure);");
            sb.AppendLine("            case global::System.Net.HttpStatusCode.Conflict:");
            sb.AppendLine("                return TypedResults.Conflict(outcome.Failure);");
            sb.AppendLine("            case global::System.Net.HttpStatusCode.Unauthorized:");
            sb.AppendLine("                return TypedResults.Unauthorized();");
            sb.AppendLine("            case global::System.Net.HttpStatusCode.Forbidden:");
            sb.AppendLine("                return TypedResults.Forbid();");
            sb.AppendLine("            default:");
            sb.AppendLine("            {");
            sb.AppendLine("                // Fallback for arbitrary status codes. If there's no failure payload, return just the status code.");
            sb.AppendLine("                if (outcome.Failure is null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    return Results.StatusCode((int)outcome.Status);");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                return Results.Json(outcome.Failure, statusCode: (int)outcome.Status);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("\"", "\"\"");

    private static bool IsFeatureContract(INamedTypeSymbol i)
    {
        if (i is not { Name: "IFeature", Arity: 5 }) return false;
        return i.ContainingNamespace is { IsGlobalNamespace: false } &&
               i.ContainingNamespace.ToDisplayString() == "WebApi.Features";
    }

    private static string Sanitize(string fqn)
    {
        var sb = new StringBuilder(fqn.Length);
        foreach (var ch in fqn)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return sb.ToString();
    }

    private sealed class EndpointModel
    {
        private EndpointModel(
            bool isFeature,
            string? endpointType,
            string? featureType,
            string? featureName,
            string? handlerType,
            string? requestBodyType,
            string? responseBodyType,
            string? commandType,
            string? resultType,
            string route,
            int method,
            string name)
        {
            IsFeature = isFeature;
            EndpointType = endpointType;
            FeatureType = featureType;
            FeatureName = featureName;
            HandlerType = handlerType;
            RequestBodyType = requestBodyType;
            ResponseBodyType = responseBodyType;
            CommandType = commandType;
            ResultType = resultType;
            Route = route;
            Method = method;
            Name = name;
        }

        public static EndpointModel ForDirectEndpoint(string endpointType, string route, int method, string name) =>
            new(
                isFeature: false,
                endpointType: endpointType,
                featureType: null,
                featureName: null,
                handlerType: null,
                requestBodyType: null,
                responseBodyType: null,
                commandType: null,
                resultType: null,
                route: route,
                method: method,
                name: name
            );

        public static EndpointModel ForFeature(
            string featureType,
            string featureName,
            string handlerType,
            string requestBodyType,
            string responseBodyType,
            string commandType,
            string resultType,
            string route,
            int method,
            string name) =>
            new(
                isFeature: true,
                endpointType: null,
                featureType: featureType,
                featureName: featureName,
                handlerType: handlerType,
                requestBodyType: requestBodyType,
                responseBodyType: responseBodyType,
                commandType: commandType,
                resultType: resultType,
                route: route,
                method: method,
                name: name
            );

        public bool IsFeature { get; }
        public string? EndpointType { get; }
        public string? FeatureType { get; }
        public string? FeatureName { get; }
        public string? HandlerType { get; }
        public string? RequestBodyType { get; }
        public string? ResponseBodyType { get; }
        public string? CommandType { get; }
        public string? ResultType { get; }
        public string Route { get; }
        public int Method { get; }
        public string Name { get; }
    }
}