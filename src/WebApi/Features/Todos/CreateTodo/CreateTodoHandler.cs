using Hangfire;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Features.Todos.SendTodoDueReminder;
using WebApi.Telemetry;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.CreateTodo;

[Service(ServiceLifetime.Transient)]
internal sealed class CreateTodoHandler(TodoContext context, IBackgroundJobClient jobs)
{
    public class Command
    {
        public string Title { get; set; } = null!;
        public DateTime? DueBy { get; set; }
    }

    public class Result
    {
        public TodoId Id { get; set; }
    }

    public async Task<Result> HandleAsync(Command request, CancellationToken ct)
    {
        var todo = new Todo
        {
            Id = TodoId.New(),
            Title = request.Title,
            DueBy = request.DueBy,
            IsComplete = false
        };

        await context.Todos.AddAsync(todo, ct);
        await context.SaveChangesAsync(ct);

        TelemetryConfig.RecordTodoCreated();

        if (todo.DueBy is { } dueBy)
        {
            // Delayed job: scheduled to fire exactly when the todo becomes due. Hangfire clamps
            // past-due schedules to run immediately, so no extra guard is needed here.
            jobs.Schedule<SendTodoDueReminderHandler>(
                j => j.HandleAsync(new SendTodoDueReminderHandler.Command { TodoId = todo.Id }, CancellationToken.None),
                new DateTimeOffset(dueBy, TimeSpan.Zero));
        }

        return new Result { Id = todo.Id };
    }
}