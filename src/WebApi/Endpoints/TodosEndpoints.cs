using WebApi.Endpoints.Todos;

namespace WebApi.Endpoints;

public static class TodosEndpoints
{
    public static void MapTodos(this IEndpointRouteBuilder app)
    {
        app.MapPost(Routes.Todos.Create, CreateTodoEndpoint.Handle).WithName("CreateTodo");
        app.MapPost(Routes.Todos.GetById, GetTodoByIdEndpoint.Handle).WithName("GetTodoById");
    }
}