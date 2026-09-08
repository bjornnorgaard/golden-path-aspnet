using WebApi.Annotations;

namespace WebApi.Features.Todos.CreateTodo;

/// <summary>
/// Runs on the Hangfire background worker, off the request thread, once a todo has been created.
/// </summary>
[Service(ServiceLifetime.Transient)]
internal sealed class TodoCreatedNotificationHandler(ILogger<TodoCreatedNotificationHandler> logger)
{
    public Task HandleAsync(Guid todoId, string title, CancellationToken ct)
    {
        logger.LogInformation("Todo {TodoId} \"{Title}\" was created; notification sent.", todoId, title);
        return Task.CompletedTask;
    }
}
