using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators;

[Generator]
public sealed class ActivityTagExtensionsGenerator : IIncrementalGenerator
{
    private const string TagKeyAttrFqn = "Platform.Annotations.TagKeyAttribute";

    private static readonly DiagnosticDescriptor MissingTagValueTypeAttribute = new(
        id: "GP0001",
        title: "Missing TagKeyAttribute",
        messageFormat: "Tag key '{0}' must declare a [TagKey(typeof(...))] attribute to generate an Activity tag setter.",
        category: "GoldenPath.Telemetry",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tagKeysTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { Identifier.Text: "TelemetryTagKeys" },
                transform: static (syntaxContext, _) =>
                {
                    var classDecl = (ClassDeclarationSyntax)syntaxContext.Node;
                    if (!classDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        return null;
                    }

                    var symbol = syntaxContext.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                    if (symbol == null)
                    {
                        return null;
                    }

                    if (!string.Equals(symbol.Name, "TelemetryTagKeys", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    var keys = new List<TagKeyModel>();
                    var diagnostics = new List<Diagnostic>();

                    foreach (var member in symbol.GetMembers().OfType<IFieldSymbol>())
                    {
                        if (!member.IsConst)
                        {
                            continue;
                        }

                        if (member.Type.SpecialType != SpecialType.System_String)
                        {
                            continue;
                        }

                        if (member.ConstantValue is not string key || string.IsNullOrWhiteSpace(key))
                        {
                            continue;
                        }

                        if (!TryResolveValueType(member, out var valueType))
                        {
                            var location = member.Locations.FirstOrDefault();
                            if (location != null)
                            {
                                diagnostics.Add(Diagnostic.Create(MissingTagValueTypeAttribute, location, member.Name));
                            }

                            continue;
                        }

                        var strongIdType = TryResolveStrongIdType(member);
                        keys.Add(new TagKeyModel(member.Name, key, valueType, strongIdType));
                    }

                    if (diagnostics.Count > 0)
                    {
                        return new TagKeysTypeModel(symbol, keys, diagnostics);
                    }

                    if (keys.Count == 0)
                    {
                        return null;
                    }

                    return new TagKeysTypeModel(symbol, keys, diagnostics);
                })
            .Where(static m => m != null);

        context.RegisterSourceOutput(tagKeysTypes, static (ctx, model) =>
        {
            foreach (var diag in model!.Diagnostics)
            {
                ctx.ReportDiagnostic(diag);
            }

            if (model.Keys.Count == 0)
            {
                return;
            }

            ctx.AddSource(
                hintName: $"{SanitizeHintName(model!.Namespace)}TelemetryTagKeys.ActivityTags.g.cs",
                sourceText: SourceText.From(Emit(model!), Encoding.UTF8));
        });
    }

    private static bool TryResolveValueType(IFieldSymbol field, out string valueTypeDisplay)
    {
        var attr = field.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == TagKeyAttrFqn);

        if (attr == null)
        {
            valueTypeDisplay = string.Empty;
            return false;
        }

        if (attr.ConstructorArguments.Length >= 1 &&
            attr.ConstructorArguments[0].Value is ITypeSymbol typeSymbol)
        {
            valueTypeDisplay = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return true;
        }

        valueTypeDisplay = string.Empty;
        return false;
    }

    private static string? TryResolveStrongIdType(IFieldSymbol field)
    {
        var attr = field.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == TagKeyAttrFqn);

        if (attr == null)
        {
            return null;
        }

        if (attr.ConstructorArguments.Length >= 2 &&
            attr.ConstructorArguments[1].Value is ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        return null;
    }

    private static string Emit(TagKeysTypeModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using global::System.Diagnostics;");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine("public static class ActivityTagExtensions");
        sb.AppendLine("{");

        foreach (var key in model.Keys.OrderBy(k => k.PropertyName, StringComparer.Ordinal))
        {
            var methodName = "Set" + key.PropertyName;
            var paramName = ToParamName(key.PropertyName);
            var tagKeyLiteral = EscapeStringLiteral(key.TagKey);

            // Activity? (nullable) overload to allow Activity.Current?.SetX(...)
            sb.AppendLine($"    public static void {methodName}(this Activity? activity, {key.ValueTypeDisplay} {paramName})");
            sb.AppendLine("    {");
            sb.AppendLine($"        activity?.SetTag(\"{tagKeyLiteral}\", {paramName});");
            sb.AppendLine("    }");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(key.StrongIdTypeDisplay))
            {
                var strongParamName = ToParamName(key.PropertyName);
                sb.AppendLine($"    public static void {methodName}(this Activity? activity, {key.StrongIdTypeDisplay} {strongParamName})");
                sb.AppendLine("    {");
                sb.AppendLine($"        activity?.SetTag(\"{tagKeyLiteral}\", {strongParamName}.Value);");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ToParamName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return "value";
        }

        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string SanitizeHintName(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(ns.Length + 1);
        foreach (var ch in ns)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        sb.Append('_');
        return sb.ToString();
    }

    private sealed class TagKeysTypeModel
    {
        public TagKeysTypeModel(INamedTypeSymbol symbol, List<TagKeyModel> keys, List<Diagnostic> diagnostics)
        {
            Namespace = symbol.ContainingNamespace is { IsGlobalNamespace: false }
                ? symbol.ContainingNamespace.ToDisplayString()
                : string.Empty;
            Keys = keys;
            Diagnostics = diagnostics;
        }

        public string Namespace { get; }
        public List<TagKeyModel> Keys { get; }
        public List<Diagnostic> Diagnostics { get; }
    }

    private sealed class TagKeyModel
    {
        public TagKeyModel(string propertyName, string tagKey, string valueTypeDisplay, string? strongIdTypeDisplay)
        {
            PropertyName = propertyName;
            TagKey = tagKey;
            ValueTypeDisplay = valueTypeDisplay;
            StrongIdTypeDisplay = strongIdTypeDisplay;
        }

        public string PropertyName { get; }
        public string TagKey { get; }
        public string ValueTypeDisplay { get; }
        public string? StrongIdTypeDisplay { get; }
    }
}