using System.Net;

namespace WebApi.Features;

public sealed record Failure(string Message, object? Details = null);

public readonly struct Outcome<T>
{
    private Outcome(HttpStatusCode status, T? value, Failure? failure)
    {
        Status = status;
        Value = value;
        Failure = failure;
    }

    public HttpStatusCode Status { get; }
    public T? Value { get; }
    public Failure? Failure { get; }

    public static Outcome<T> Ok(T value)
    {
        return new Outcome<T>(HttpStatusCode.OK, value, failure: null);
    }

    public static Outcome<T> Fail(HttpStatusCode status, string message, object? details = null)
    {
        return new Outcome<T>(status, value: default, new Failure(message, details));
    }

    public static Outcome<T> BadRequest(string message, object? details = null)
    {
        return Fail(HttpStatusCode.BadRequest, message, details);
    }

    public static Outcome<T> NotFound(string message, object? details = null)
    {
        return Fail(HttpStatusCode.NotFound, message, details);
    }

    public static Outcome<T> InternalServerError(string message, object? details = null)
    {
        return Fail(HttpStatusCode.InternalServerError, message, details);
    }
}