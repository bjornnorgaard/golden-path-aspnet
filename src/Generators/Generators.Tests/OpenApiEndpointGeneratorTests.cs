using Generators.OpenApi;
using Generators.GraphQl;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Generators.Tests;

public sealed class OpenApiEndpointGeneratorTests
{
    [Test]
    public async Task Generates_interface_and_typed_results()
    {
        var result = Run("""
openapi: 3.1.0
paths:
  /todos:
    post:
      operationId: createTodo
      requestBody:
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreateTodoRequest'
      responses:
        '200':
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CreateTodoResponse'
        '400':
          content:
            application/problem+json:
              schema:
                $ref: '#/components/schemas/ProblemDetails'
components:
  schemas:
    CreateTodoRequest:
      type: object
      properties:
        title:
          type: string
    CreateTodoResponse:
      type: object
      properties:
        id:
          type: string
    ProblemDetails:
      type: object
      properties:
        title:
          type: string
""", "public sealed class Handler : ICreateTodoEndpoint { }");
        await Assert.That(result.Generated).Contains("public interface ICreateTodoEndpoint");
        await Assert.That(result.Generated).Contains("Results<global::Microsoft.AspNetCore.Http.HttpResults.Ok<global::WebApi.Todos.Contracts.CreateTodoResponse>, global::Microsoft.AspNetCore.Http.HttpResults.BadRequest<global::WebApi.Todos.Contracts.ProblemDetails>>");
        await Assert.That(result.Generated).Contains("global::FluentValidation.IValidator<global::WebApi.Todos.Contracts.CreateTodoRequest> validator");
        await Assert.That(result.Generated).Contains("await validator.ValidateAsync(request, ct)");
        await Assert.That(result.Generated).Contains("public sealed class CreateTodoRequestValidator");
    }

    [Test]
    public async Task Reports_missing_implementation()
    {
        var result = Run("""
openapi: 3.1.0
paths:
  /todos:
    post:
      operationId: createTodo
      responses:
        '200': { description: ok }
""", "namespace Example;");
        await Assert.That(result.Diagnostics).Contains("GP2001");
    }

    [Test]
    public async Task Generates_graphql_queries_mutations_and_handler_dispatch()
    {
        const string spec = """
type Query {
  getTodoList(input: GetTodoListRequest!): GetTodoListResponse!
}

type Mutation {
  createTodo(input: CreateTodoRequest!): CreateTodoResponse!
}
""";
        var compilation = CSharpCompilation.Create("Example.Api", [CSharpSyntaxTree.ParseText("namespace Example;")], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new GraphQlEndpointGenerator().AsSourceGenerator()], [new TextFile("todos.graphql", spec)]);
        var generated = string.Join("\n", driver.RunGenerators(compilation).GetRunResult().GeneratedTrees.Select(tree => tree.GetText().ToString()));

        await Assert.That(generated).Contains("AddGeneratedGraphQlEndpoints");
        await Assert.That(generated).Contains("namespace Example.Api.Todos.GraphQl");
        await Assert.That(generated).Contains("endpoints.MapGraphQL(\"/graphql\")");
        await Assert.That(generated).Contains("GeneratedTodosGraphQlQuery");
        await Assert.That(generated).Contains("GeneratedTodosGraphQlMutation");
        await Assert.That(generated).Contains("public interface IGetTodoListResolver");
        await Assert.That(generated).Contains("public interface ICreateTodoResolver");
        await Assert.That(generated).Contains("IGetTodoListResolver resolver");
        await Assert.That(generated).Contains("new(\"GoldenPath.GraphQL\")");
        await Assert.That(generated).Contains("Activity.Current?.SetTag(\"graphql.operation.name\", operationName)");
        await Assert.That(generated).Contains("const string operationName = \"graphql.query.getTodoList\"");
        await Assert.That(generated).Contains("const string operationName = \"graphql.mutation.createTodo\"");
    }

    [Test]
    public async Task Reports_missing_graphql_resolver_implementation()
    {
        const string spec = """
type Query {
  getTodoList(input: GetTodoListRequest!): GetTodoListResponse!
}
""";
        var compilation = CSharpCompilation.Create("WebApi", [CSharpSyntaxTree.ParseText("namespace Example;")], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new GraphQlEndpointGenerator().AsSourceGenerator()], [new TextFile("todos.graphql", spec)]);
        var diagnostics = string.Join("\n", driver.RunGenerators(compilation).GetRunResult().Diagnostics.Select(diagnostic => diagnostic.Id));

        await Assert.That(diagnostics).Contains("GP3000");
    }

    private static (string Generated, string Diagnostics) Run(string spec, string source)
    {
        var compilation = CSharpCompilation.Create("WebApi", [CSharpSyntaxTree.ParseText(source)], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new OpenApiEndpointGenerator().AsSourceGenerator()], [new TextFile("todos.openapi.yaml", spec)]);
        driver = driver.RunGenerators(compilation);
        var run = driver.GetRunResult();
        return (string.Join("\n", run.GeneratedTrees.Select(tree => tree.GetText().ToString())), string.Join("\n", run.Diagnostics.Select(diagnostic => diagnostic.Id)));
    }

    private sealed class TextFile(string path, string text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
