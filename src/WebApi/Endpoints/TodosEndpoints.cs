using WebApi.Endpoints.Todos;

namespace WebApi.Endpoints;

public static class TodosEndpoints
{
    public static IEndpointRouteBuilder MapTodos(this IEndpointRouteBuilder app)
    {
        app.MapPost(Routes.Todos.Create, CreateTodoEndpoint.Handle)
            .WithName("CreateTodo");

        app.MapGet(Routes.Todos.GetById, GetTodoByIdEndpoint.Handle)
            .WithName("GetTodoById");
        return app;
    }
}