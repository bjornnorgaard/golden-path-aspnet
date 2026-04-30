using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class StrongIdAttribute(string underlying = "System.Guid") : Attribute
{
    public string Underlying { get; } = underlying;
}

[Generator]
public class StrongIdGenerator : IIncrementalGenerator
{
    private const string AttrFqn = "Platform.Annotations.StrongIdAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.ForAttributeWithMetadataName<StrongIdModel>(
            AttrFqn,
            predicate: static (node, _) => node is StructDeclarationSyntax s && s.Modifiers.Any(SyntaxKind.PartialKeyword),
            transform: static (syntaxContext, _) =>
            {
                var symbol = syntaxContext.TargetSymbol;
                var attr = syntaxContext.Attributes[0];

                var underlyingArg = "System.Guid";
                var value = attr.ConstructorArguments[0].Value;
                if (attr.ConstructorArguments.Length == 1 && value is not null)
                {
                    underlyingArg = value.ToString();
                }

                return new StrongIdModel
                {
                    Namespace = symbol.ContainingNamespace.ToDisplayString(),
                    Name = symbol.Name,
                    Accessibility = symbol.DeclaredAccessibility,
                    UnderlyingTypeDisplay = underlyingArg
                };
            }
        );

        var models = provider.Collect();

        context.RegisterSourceOutput(models, static (ctx, batch) =>
        {
            foreach (var model in batch)
            {
                ctx.AddSource($"{model.Name}.StrongId.g.cs", EmitSource(model));
            }
        });
    }

    private static SourceText EmitSource(StrongIdModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"public partial struct {model.Name} : StrongId<{model.UnderlyingTypeDisplay}>");
        sb.AppendLine("{");
        sb.AppendLine("}");
        return SourceText.From(sb.ToString(), Encoding.UTF8);
    }
}

public class StrongIdModel
{
    public string Namespace { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Accessibility Accessibility { get; set; }
    public string UnderlyingTypeDisplay { get; set; } = null!;
}