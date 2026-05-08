using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Generators;

[Generator]
public sealed class AppSettingsConfigurationGenerator : IIncrementalGenerator
{
    private const string AppSettingsFileName = "appsettings.json";

    private static readonly DiagnosticDescriptor AppSettingsFileNotFound = new(
        id: "GP1000",
        title: "Missing appsettings.json for configuration generation",
        messageFormat: "No '{0}' was provided to the source generator. Add '<AdditionalFiles Include=\"appsettings.json\" />' to the project file.",
        category: "GoldenPath.Configuration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NullValueNotAllowed = new(
        id: "GP1001",
        title: "Null configuration values are not allowed",
        messageFormat: "Configuration key '{0}' in appsettings.json must not be null",
        category: "GoldenPath.Configuration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var appSettings = context.AdditionalTextsProvider
            .Where(static at => string.Equals(Path.GetFileName(at.Path), AppSettingsFileName, StringComparison.OrdinalIgnoreCase))
            .Select(static (at, ct) => (path: at.Path, text: at.GetText(ct)?.ToString()))
            .Where(static t => !string.IsNullOrWhiteSpace(t.text));

        var combined = context.CompilationProvider.Combine(appSettings.Collect());

        context.RegisterSourceOutput(combined, static (ctx, t) =>
        {
            var (compilation, files) = t;
            var assemblyName = compilation.AssemblyName ?? "App";

            var file = files
                .OrderBy(static f => f.path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(file.text))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(AppSettingsFileNotFound, location: null, AppSettingsFileName));
                return;
            }

            var parse = TryParseRootObject(file.text!, out var root, out var error);
            if (!parse)
            {
                ctx.AddSource(
                    "AppSettingsConfiguration.Errors.g.cs",
                    SourceText.From(EmitError(assemblyName, error ?? "Unable to parse appsettings.json."), Encoding.UTF8));
                return;
            }

            var rootNamespace = assemblyName + ".Configuration";
            var schema = BuildSchema(rootNamespace, root);

            foreach (var keyPath in FindNullValuePaths(root))
            {
                // We don't have a precise Location in JSON. Report without location (still fails build).
                ctx.ReportDiagnostic(Diagnostic.Create(NullValueNotAllowed, location: null, keyPath));
            }

            ctx.AddSource(
                "AppSettingsConfiguration.Types.g.cs",
                SourceText.From(EmitTypes(rootNamespace, schema), Encoding.UTF8));

            ctx.AddSource(
                "AppSettingsConfiguration.DependencyInjection.g.cs",
                SourceText.From(EmitDependencyInjection(assemblyName, rootNamespace, schema), Encoding.UTF8));

            ctx.AddSource(
                "AppSettingsConfiguration.ConfigurationExtensions.g.cs",
                SourceText.From(EmitConfigurationExtensions(rootNamespace, schema), Encoding.UTF8));
        });
    }

    private static bool TryParseRootObject(string json, out JsonElement rootObject, out string? error)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                rootObject = default;
                error = "appsettings.json root must be a JSON object.";
                return false;
            }

            rootObject = doc.RootElement.Clone();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            rootObject = default;
            error = ex.Message;
            return false;
        }
    }

    private static AppSettingsSchema BuildSchema(string rootNamespace, JsonElement rootObject)
    {
        var types = new Dictionary<string, ConfigType>(StringComparer.Ordinal);
        var topLevels = new List<TopLevelSection>();

        foreach (var prop in rootObject.EnumerateObject())
        {
            var sectionName = prop.Name;
            var typeName = ToPascalIdentifier(sectionName);
            if (string.IsNullOrWhiteSpace(typeName))
            {
                typeName = "Section";
            }

            typeName = EnsureOptionsTypeName(typeName);

            var fullName = rootNamespace + "." + typeName;
            var type = EnsureType(types, fullName);
            PopulateObjectType(types, rootNamespace, type, prop.Value, preferredNestedNamePrefix: typeName);

            topLevels.Add(new TopLevelSection(sectionName, typeName, fullName));
        }

        return new AppSettingsSchema(rootNamespace, topLevels.ToImmutableArray(), types.Values.OrderBy(t => t.FullName, StringComparer.Ordinal).ToImmutableArray());
    }

    private static ConfigType EnsureType(Dictionary<string, ConfigType> types, string fullName)
    {
        if (types.TryGetValue(fullName, out var existing))
        {
            return existing;
        }

        var t = new ConfigType(fullName);
        types[fullName] = t;
        return t;
    }

    private static void PopulateObjectType(
        Dictionary<string, ConfigType> types,
        string rootNamespace,
        ConfigType type,
        JsonElement value,
        string preferredNestedNamePrefix)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            // If the section isn't an object, still allow binding to a wrapper class with a single Value property.
            type.Properties.Clear();
            type.Properties.Add(new ConfigProperty("Value", ResolvePrimitiveCSharpType(value), jsonPath: preferredNestedNamePrefix + ":Value", jsonKey: "Value"));
            return;
        }

        foreach (var prop in value.EnumerateObject())
        {
            var propName = ToPascalIdentifier(prop.Name);
            if (string.IsNullOrWhiteSpace(propName))
            {
                continue;
            }

            var propValue = prop.Value;
            if (propValue.ValueKind == JsonValueKind.Object)
            {
                var nestedNameBase = StripOptionsSuffix(preferredNestedNamePrefix) + propName;
                var nestedName = EnsureOptionsTypeName(nestedNameBase);
                var nestedFullName = rootNamespace + "." + nestedName;
                var nestedType = EnsureType(types, nestedFullName);
                PopulateObjectType(types, rootNamespace, nestedType, propValue, preferredNestedNamePrefix: nestedName);

                type.Properties.Add(new ConfigProperty(propName, "global::" + nestedFullName, jsonPath: preferredNestedNamePrefix + ":" + prop.Name, jsonKey: prop.Name));
            }
            else if (propValue.ValueKind == JsonValueKind.Array)
            {
                var elemType = ResolveArrayElementType(types, rootNamespace, StripOptionsSuffix(preferredNestedNamePrefix) + propName, propValue);
                type.Properties.Add(new ConfigProperty(propName, $"global::System.Collections.Generic.List<{elemType}>", jsonPath: preferredNestedNamePrefix + ":" + prop.Name, jsonKey: prop.Name));
            }
            else
            {
                type.Properties.Add(new ConfigProperty(propName, ResolvePrimitiveCSharpType(propValue), jsonPath: preferredNestedNamePrefix + ":" + prop.Name, jsonKey: prop.Name));
            }
        }
    }

    private static string ResolveArrayElementType(
        Dictionary<string, ConfigType> types,
        string rootNamespace,
        string nestedNamePrefix,
        JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var nestedName = EnsureOptionsTypeName(nestedNamePrefix + "Item");
                var nestedFullName = rootNamespace + "." + nestedName;
                var nestedType = EnsureType(types, nestedFullName);
                PopulateObjectType(types, rootNamespace, nestedType, item, preferredNestedNamePrefix: nestedName);
                return "global::" + nestedFullName;
            }

            if (item.ValueKind != JsonValueKind.Null && item.ValueKind != JsonValueKind.Undefined)
            {
                return ResolvePrimitiveCSharpType(item);
            }
        }

        // If array is empty or only nulls, treat as string elements (non-nullable schema).
        return "global::System.String";
    }

    private static string ResolvePrimitiveCSharpType(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => "global::System.String",
            JsonValueKind.True => "global::System.Boolean",
            JsonValueKind.False => "global::System.Boolean",
            JsonValueKind.Number => ResolveNumberType(value),
            // Schema is non-nullable. Nulls are reported as a generator error diagnostic.
            JsonValueKind.Null => "global::System.String",
            JsonValueKind.Undefined => "global::System.String",
            _ => "global::System.String"
        };

    private static string ResolveNumberType(JsonElement value)
    {
        if (value.TryGetInt32(out _)) return "global::System.Int32";
        if (value.TryGetInt64(out _)) return "global::System.Int64";
        if (value.TryGetDouble(out _)) return "global::System.Double";
        return "global::System.Decimal";
    }

    private static string EmitTypes(string rootNamespace, AppSettingsSchema schema)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();

        foreach (var type in schema.Types)
        {
            if (!type.FullName.StartsWith(rootNamespace + ".", StringComparison.Ordinal))
            {
                continue;
            }

            var name = type.FullName.Substring(rootNamespace.Length + 1);
            if (name.Contains('.'))
            {
                // We only emit one-level types in the target namespace (we never generate nested namespaces).
                continue;
            }

            sb.AppendLine($"public sealed class {name}");
            sb.AppendLine("{");

            foreach (var prop in type.Properties
                         .OrderBy(static p => p.Name, StringComparer.Ordinal))
            {
                if (!string.Equals(prop.JsonKey, prop.Name, StringComparison.Ordinal))
                {
                    sb.AppendLine($"    [global::Microsoft.Extensions.Configuration.ConfigurationKeyName(\"{EscapeStringLiteral(prop.JsonKey)}\")]");
                }
                sb.AppendLine($"    public required {prop.CSharpType} {prop.Name} {{ get; set; }} = default!;");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EmitDependencyInjection(string assemblyName, string rootNamespace, AppSettingsSchema schema)
    {
        var methodName = "Add" + ToPascalIdentifier(assemblyName) + "GeneratedConfiguration";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("using global::System.Collections.Generic;");
        sb.AppendLine("public static partial class GeneratedConfigurationRegistrationExtensions");
        sb.AppendLine("{");
        sb.AppendLine($"    public static global::Microsoft.AspNetCore.Builder.WebApplicationBuilder {methodName}(");
        sb.AppendLine("        this global::Microsoft.AspNetCore.Builder.WebApplicationBuilder builder)");
        sb.AppendLine("    {");
        sb.AppendLine($"        builder.Services.{methodName}(builder.Configuration);");
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection {methodName}(");
        sb.AppendLine("        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services,");
        sb.AppendLine("        global::Microsoft.Extensions.Configuration.IConfiguration configuration)");
        sb.AppendLine("    {");

        foreach (var section in schema.TopLevelSections.OrderBy(s => s.SectionName, StringComparer.Ordinal))
        {
            var typeFqn = "global::" + section.TypeFullName;
            var sectionLiteral = EscapeStringLiteral(section.SectionName);
            var validatorType = "__ValidateOptions_" + section.TypeName;

            sb.AppendLine($"        services.AddOptions<{typeFqn}>()");
            sb.AppendLine($"            .Bind(configuration.GetSection(\"{sectionLiteral}\"))");
            sb.AppendLine("            .ValidateOnStart();");
            sb.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeFqn}>>(new {validatorType}());");
            sb.AppendLine($"        services.AddSingleton(sp => sp.GetRequiredService<global::Microsoft.Extensions.Options.IOptions<{typeFqn}>>().Value);");
            sb.AppendLine();
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine();
        // Strongly-typed null validation helpers (AOT-friendly: no reflection).
        foreach (var section in schema.TopLevelSections.OrderBy(s => s.SectionName, StringComparer.Ordinal))
        {
            EmitValidatorForType(sb, rootNamespace, schema, section.TypeName, section.TypeFullName);
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitValidatorForType(StringBuilder sb, string rootNamespace, AppSettingsSchema schema, string rootTypeName, string rootTypeFullName)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        EmitValidatorForTypeInner(sb, rootNamespace, schema, rootTypeName, rootTypeFullName, emitted);
        EmitValidateOptionsType(sb, rootNamespace, schema, rootTypeName, rootTypeFullName);
    }

    private static void EmitValidateOptionsType(StringBuilder sb, string rootNamespace, AppSettingsSchema schema, string rootTypeName, string rootTypeFullName)
    {
        sb.AppendLine($"    private sealed class __ValidateOptions_{rootTypeName} : global::Microsoft.Extensions.Options.IValidateOptions<global::{rootTypeFullName}>");
        sb.AppendLine("    {");
        sb.AppendLine("        public global::Microsoft.Extensions.Options.ValidateOptionsResult Validate(string? name, global::" + rootTypeFullName + " options)");
        sb.AppendLine("        {");
        sb.AppendLine("            var errors = new global::System.Collections.Generic.List<string>();");
        sb.AppendLine($"            CollectNullPaths_{rootTypeName}(options, errors);");
        sb.AppendLine("            if (errors.Count == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                return global::Microsoft.Extensions.Options.ValidateOptionsResult.Success;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return global::Microsoft.Extensions.Options.ValidateOptionsResult.Fail(");
        sb.AppendLine("                \"Configuration contains null values: \" + string.Join(\", \", errors));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitValidatorForTypeInner(
        StringBuilder sb,
        string rootNamespace,
        AppSettingsSchema schema,
        string rootTypeName,
        string rootTypeFullName,
        HashSet<string> emitted)
    {
        // Emit a validator for every generated type in the root namespace that is reachable from the top-level section.
        // We keep it simple: generate validators on-demand as we walk properties.
        if (!emitted.Add(rootTypeFullName))
        {
            return;
        }

        var type = schema.Types.FirstOrDefault(t => string.Equals(t.FullName, rootTypeFullName, StringComparison.Ordinal));
        if (type == null)
        {
            // Shouldn't happen, but keep generated code compilable.
            sb.AppendLine($"    private static void CollectNullPaths_{rootTypeName}(global::{rootTypeFullName} value, global::System.Collections.Generic.List<string> errors) {{ }}");
            return;
        }

        sb.AppendLine($"    private static void CollectNullPaths_{rootTypeName}(global::{rootTypeFullName} value, global::System.Collections.Generic.List<string> errors)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine($"            errors.Add(\"{EscapeStringLiteral(rootTypeName)}\");");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");

        foreach (var prop in type.Properties.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            EmitNullCheckForProperty(sb, rootNamespace, schema, rootTypeName, prop);
        }

        sb.AppendLine("    }");

        // Recurse into nested generated types referenced by properties.
        foreach (var prop in type.Properties)
        {
            var nestedTypeFullName = TryGetNestedGeneratedTypeFullName(rootNamespace, prop.CSharpType);
            if (nestedTypeFullName != null)
            {
                var nestedTypeName = nestedTypeFullName.Substring(rootNamespace.Length + 1);
                EmitValidatorForTypeInner(sb, rootNamespace, schema, nestedTypeName, nestedTypeFullName, emitted);
            }

            var listElemType = TryGetListElementType(prop.CSharpType);
            var nestedElemTypeFullName = listElemType != null ? TryGetNestedGeneratedTypeFullName(rootNamespace, listElemType) : null;
            if (nestedElemTypeFullName != null)
            {
                var nestedTypeName = nestedElemTypeFullName.Substring(rootNamespace.Length + 1);
                EmitValidatorForTypeInner(sb, rootNamespace, schema, nestedTypeName, nestedElemTypeFullName, emitted);
            }
        }
    }

    private static void EmitNullCheckForProperty(StringBuilder sb, string rootNamespace, AppSettingsSchema schema, string currentTypeName, ConfigProperty prop)
    {
        var type = prop.CSharpType;
        var propExpr = $"value.{prop.Name}";
        var jsonPathLiteral = EscapeStringLiteral(prop.JsonPath);

        if (string.Equals(type, "global::System.String", StringComparison.Ordinal))
        {
            sb.AppendLine($"        if ({propExpr} is null) errors.Add(\"{jsonPathLiteral}\");");
            return;
        }

        if (type.StartsWith("global::System.Collections.Generic.List<", StringComparison.Ordinal))
        {
            var elemType = TryGetListElementType(type) ?? "global::System.String";
            sb.AppendLine($"        if ({propExpr} is null) errors.Add(\"{jsonPathLiteral}\");");
            sb.AppendLine($"        if ({propExpr} is not null)");
            sb.AppendLine("        {");
            sb.AppendLine($"            for (var i = 0; i < {propExpr}.Count; i++)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var item = {propExpr}[i];");
            sb.AppendLine($"                if (item is null) errors.Add($\"{jsonPathLiteral}[{{i}}]\");");

            var nestedElemTypeFullName = TryGetNestedGeneratedTypeFullName(rootNamespace, elemType);
            if (nestedElemTypeFullName != null)
            {
                var nestedName = nestedElemTypeFullName.Substring(rootNamespace.Length + 1);
                sb.AppendLine($"                if (item is not null) CollectNullPaths_{nestedName}(item, errors);");
            }

            sb.AppendLine("            }");
            sb.AppendLine("        }");
            return;
        }

        // Value types are always non-null.
        if (type.StartsWith("global::System.", StringComparison.Ordinal) &&
            !string.Equals(type, "global::System.String", StringComparison.Ordinal))
        {
            return;
        }

        // Generated complex type: ensure not null and validate nested.
        var nestedTypeFullName = TryGetNestedGeneratedTypeFullName(rootNamespace, type);
        if (nestedTypeFullName != null)
        {
            var nestedName = nestedTypeFullName.Substring(rootNamespace.Length + 1);
            sb.AppendLine($"        if ({propExpr} is null) errors.Add(\"{jsonPathLiteral}\");");
            sb.AppendLine($"        if ({propExpr} is not null) CollectNullPaths_{nestedName}({propExpr}, errors);");
        }
        else
        {
            // Unknown reference type, still null-check.
            sb.AppendLine($"        if ({propExpr} is null) errors.Add(\"{jsonPathLiteral}\");");
        }
    }

    private static string? TryGetListElementType(string type)
    {
        const string prefix = "global::System.Collections.Generic.List<";
        if (!type.StartsWith(prefix, StringComparison.Ordinal)) return null;
        if (!type.EndsWith(">", StringComparison.Ordinal)) return null;
        return type.Substring(prefix.Length, type.Length - prefix.Length - 1);
    }

    private static string? TryGetNestedGeneratedTypeFullName(string rootNamespace, string type)
    {
        const string globalPrefix = "global::";
        if (!type.StartsWith(globalPrefix, StringComparison.Ordinal)) return null;
        var fqn = type.Substring(globalPrefix.Length);
        if (!fqn.StartsWith(rootNamespace + ".", StringComparison.Ordinal)) return null;
        return fqn;
    }

    private static string EmitConfigurationExtensions(string rootNamespace, AppSettingsSchema schema)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Microsoft.Extensions.Configuration;");
        sb.AppendLine();
        sb.AppendLine("public static partial class GeneratedConfigurationExtensions");
        sb.AppendLine("{");

        foreach (var section in schema.TopLevelSections.OrderBy(s => s.SectionName, StringComparer.Ordinal))
        {
            var typeFqn = "global::" + section.TypeFullName;
            var method = "Get" + ToPascalIdentifier(section.SectionName);
            var sectionLiteral = EscapeStringLiteral(section.SectionName);

            sb.AppendLine($"    public static {typeFqn} {method}(this global::Microsoft.Extensions.Configuration.IConfiguration configuration)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var section = configuration.GetSection(\"{sectionLiteral}\");");
            sb.AppendLine($"        return global::Microsoft.Extensions.Configuration.ConfigurationBinder.Get<{typeFqn}>(section)");
            sb.AppendLine($"            ?? throw new global::System.InvalidOperationException(\"Unable to bind required configuration section '{sectionLiteral}'.\");");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EmitError(string assemblyName, string message)
    {
        var ns = assemblyName + ".Configuration";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("internal static class AppSettingsConfigurationGeneratorErrors");
        sb.AppendLine("{");
        sb.AppendLine($"    internal const string Error = \"{EscapeStringLiteral(message)}\";");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private const string OptionsSuffix = "Options";

    private static string EnsureOptionsTypeName(string typeName) =>
        typeName.EndsWith(OptionsSuffix, StringComparison.Ordinal) ? typeName : typeName + OptionsSuffix;

    private static string StripOptionsSuffix(string typeName) =>
        typeName.EndsWith(OptionsSuffix, StringComparison.Ordinal)
            ? typeName.Substring(0, typeName.Length - OptionsSuffix.Length)
            : typeName;

    private static string ToPascalIdentifier(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        var upperNext = true;

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(upperNext ? char.ToUpperInvariant(ch) : ch);
                upperNext = false;
            }
            else
            {
                upperNext = true;
            }
        }

        var ident = sb.ToString();
        if (string.IsNullOrWhiteSpace(ident)) return string.Empty;
        if (char.IsDigit(ident[0])) ident = "_" + ident;
        return ident;
    }

    private static IEnumerable<string> FindNullValuePaths(JsonElement element)
    {
        var stack = new Stack<(string path, JsonElement el)>();
        stack.Push(("$", element));

        while (stack.Count > 0)
        {
            var (path, el) = stack.Pop();

            switch (el.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    yield return path;
                    break;
                case JsonValueKind.Object:
                    foreach (var p in el.EnumerateObject())
                    {
                        stack.Push(($"{path}:{p.Name}", p.Value));
                    }

                    break;
                case JsonValueKind.Array:
                    var idx = 0;
                    foreach (var item in el.EnumerateArray())
                    {
                        stack.Push(($"{path}[{idx}]", item));
                        idx++;
                    }

                    break;
            }
        }
    }

    private sealed class AppSettingsSchema(string rootNamespace, ImmutableArray<TopLevelSection> topLevelSections, ImmutableArray<ConfigType> types)
    {
        public string RootNamespace { get; } = rootNamespace;
        public ImmutableArray<TopLevelSection> TopLevelSections { get; } = topLevelSections;
        public ImmutableArray<ConfigType> Types { get; } = types;
    }

    private sealed class TopLevelSection(string sectionName, string typeName, string typeFullName)
    {
        public string SectionName { get; } = sectionName;
        public string TypeName { get; } = typeName;
        public string TypeFullName { get; } = typeFullName;
    }

    private sealed class ConfigType(string fullName)
    {
        public string FullName { get; } = fullName;
        public List<ConfigProperty> Properties { get; } = new();
    }

    private sealed class ConfigProperty(string name, string cSharpType, string jsonPath, string jsonKey)
    {
        public string Name { get; } = name;
        public string CSharpType { get; } = cSharpType;
        public string JsonPath { get; } = jsonPath;
        public string JsonKey { get; } = jsonKey;
    }
}