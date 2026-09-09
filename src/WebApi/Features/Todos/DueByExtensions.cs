namespace WebApi.Features.Todos;

/// <summary>
/// Helpers for translating the public, offset-aware <c>DueBy</c> due-date field into the value
/// the domain and database expect.
/// </summary>
internal static class DueByExtensions
{
    /// <summary>
    /// Converts an offset-aware due-date into the UTC-kinded value the domain stores. The time of
    /// day is preserved, since the due-date drives the delayed reminder job that fires exactly when
    /// a todo becomes due. The kind matters: PostgreSQL's timestamptz column rejects
    /// <see cref="DateTime"/> values with <see cref="DateTimeKind.Unspecified"/>.
    /// </summary>
    public static DateTime? ToUtcDueBy(this DateTimeOffset? dueBy) => dueBy?.UtcDateTime;
}
