namespace WebApi.Features.Todos;

/// <summary>
/// Helpers for translating the public, offset-aware <c>DueBy</c> due-date field into the value
/// the domain and database expect.
/// </summary>
internal static class DueByExtensions
{
    /// <summary>
    /// Converts an offset-aware due-date into a UTC-kinded, date-only value. PostgreSQL's
    /// timestamptz column rejects <see cref="DateTime"/> values with <see cref="DateTimeKind.Unspecified"/>,
    /// which is what <see cref="DateTimeOffset.Date"/> alone would produce, so the value is
    /// converted to UTC before truncating to a date.
    /// </summary>
    public static DateTime? ToUtcDueDate(this DateTimeOffset? dueBy) => dueBy?.UtcDateTime.Date;
}
