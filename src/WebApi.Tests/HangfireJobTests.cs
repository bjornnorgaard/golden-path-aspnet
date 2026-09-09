using System.Net;
using System.Net.Http.Json;
using Hangfire;
using Hangfire.Storage;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

/// <summary>
/// Exercises the three Hangfire job patterns wired into the todo lifecycle: fire-and-forget
/// (on completion), delayed (on creation with a due date), and recurring (overdue sweep).
/// Jobs are verified through Hangfire's monitoring API rather than waiting for a worker to
/// actually execute them, since job execution timing is not something the API contract should
/// have to guarantee for a test to pass reliably.
/// </summary>
public class HangfireJobTests : TestBase
{
    [Test]
    public async Task ToggleTodo_ToComplete_EnqueuesFireAndForgetCompletionNotification()
    {
        // Arrange: create an incomplete todo via the API
        var create = await Client.PostAsJsonAsync(TestRoutes.Todos.Create, new CreateTodoApiRequest
        {
            Title = "A todo that will be completed to trigger a notification job",
            DueBy = null
        });
        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<CreateTodoResponse>();
        await Assert.That(created).IsNotNull();

        var monitoring = JobStorage.Current.GetMonitoringApi();
        var jobCountBefore = CountJobsTargeting(monitoring, "SendTodoCompletedNotificationJob");

        // Act: toggle it complete
        var toggle = await Client.PostAsJsonAsync(TestRoutes.Todos.Toggle, new ToggleTodoApiRequest { Id = created!.Id });
        await Assert.That(toggle.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Assert: a fire-and-forget job targeting the notification method was created. The job
        // may be enqueued, processing, or succeeded by the time we check (a real Hangfire server
        // is running in-process and can pick it up at any moment), so every terminal/in-flight
        // state is counted, and we poll briefly since becoming visible via the monitoring API can
        // lag slightly behind the job actually being persisted/picked up.
        await WaitUntilAsync(() => CountJobsTargeting(monitoring, "SendTodoCompletedNotificationJob") > jobCountBefore);

        // Assert: the job actually executed successfully, i.e. its TodoId argument round-tripped
        // through Hangfire's job-argument (de)serializer without error.
        await WaitUntilAsync(() => CountSucceededJobs(monitoring, "SendTodoCompletedNotificationJob") > 0);
        await Assert.That(CountFailedJobs(monitoring, "SendTodoCompletedNotificationJob")).IsEqualTo(0);
    }

    [Test]
    public async Task CreateTodo_WithDueDate_SchedulesDelayedDueReminder()
    {
        // Arrange
        var monitoring = JobStorage.Current.GetMonitoringApi();
        var jobCountBefore = CountJobsTargeting(monitoring, "SendTodoDueReminderJob");

        // Act: create a todo with a future due date
        var create = await Client.PostAsJsonAsync(TestRoutes.Todos.Create, new CreateTodoApiRequest
        {
            Title = "A todo with a due date that schedules a delayed reminder job",
            DueBy = DateTime.UtcNow.AddDays(1)
        });
        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Assert: a delayed job targeting the reminder method was scheduled
        await WaitUntilAsync(() => CountJobsTargeting(monitoring, "SendTodoDueReminderJob") > jobCountBefore);
    }

    [Test]
    public async Task RecurringOverdueSweepJob_IsRegisteredOnStartup()
    {
        // Assert: the recurring job registered by UsePlatformHangfire exists in storage
        var connection = JobStorage.Current.GetConnection();
        var recurringJobs = connection.GetRecurringJobs();
        await Assert.That(recurringJobs.Any(j => j.Id == "sweep-overdue-todos")).IsTrue();
    }

    /// <summary>
    /// Counts jobs targeting the given job class (by declaring type name, since each job class
    /// exposes a single <c>InvokeAsync</c> method) across every queue/state Hangfire's monitoring
    /// API exposes (enqueued, scheduled, processing, succeeded), so the assertion holds regardless
    /// of how far the in-process Hangfire server has already progressed the job.
    /// </summary>
    private static int CountJobsTargeting(IMonitoringApi monitoring, string jobTypeName)
    {
        var count = 0;

        foreach (var queue in monitoring.Queues())
        {
            count += monitoring.EnqueuedJobs(queue.Name, 0, 1000).Count(j => j.Value.Job?.Type.Name == jobTypeName);
        }

        count += monitoring.ScheduledJobs(0, 1000).Count(j => j.Value.Job?.Type.Name == jobTypeName);
        count += monitoring.ProcessingJobs(0, 1000).Count(j => j.Value.Job?.Type.Name == jobTypeName);
        count += monitoring.SucceededJobs(0, 1000).Count(j => j.Value.Job?.Type.Name == jobTypeName);

        return count;
    }

    /// <summary>
    /// Counts succeeded jobs targeting the given job class, used to confirm a job didn't just get
    /// enqueued but actually executed without throwing (e.g. a job-argument deserialization error).
    /// </summary>
    private static int CountSucceededJobs(IMonitoringApi monitoring, string jobTypeName) =>
        monitoring.SucceededJobs(0, 1000).Count(j => j.Value.Job?.Type.Name == jobTypeName);

    /// <summary>
    /// Counts failed jobs targeting the given job class, used to catch job-argument deserialization
    /// or handler errors that would otherwise be masked by only checking for "some" job state.
    /// </summary>
    private static int CountFailedJobs(IMonitoringApi monitoring, string jobTypeName) =>
        monitoring.FailedJobs(0, 1000).Count(j => j.Value.Job?.Type.Name == jobTypeName);

    /// <summary>
    /// Polls the given condition until it becomes true or a short timeout elapses, since job
    /// visibility through Hangfire's monitoring API can lag slightly behind enqueue/schedule.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000, int pollMs = 100)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(pollMs);
        }

        await Assert.That(condition()).IsTrue();
    }
}
