using Microsoft.AspNetCore.Http.HttpResults;

namespace WebApi.Features.Todos;

/// <summary>
/// Adds todo-specific result shorthands directly onto <see cref="TypedResults"/> so they show up
/// alongside <c>TypedResults.BadRequest</c>/<c>Ok</c>/etc. in IntelliSense.
/// </summary>
internal static class TypedResultsExtensions
{
    private static readonly IReadOnlyDictionary<string, string[]> InvalidTodoIdErrors = new Dictionary<string, string[]> { ["Id"] = ["Id must be a valid UUID."] };

    extension(TypedResults)
    {
        public static BadRequest<IReadOnlyDictionary<string, string[]>> BadRequestTodoIdInvalid() => TypedResults.BadRequest(InvalidTodoIdErrors);
    }
}