using FluentValidation;
using FluentValidation.Results;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;

namespace WebApi.Services;

/// <summary>
/// The error model of the gRPC contract (see the header of todos.proto): a violated rule is
/// INVALID_ARGUMENT carrying a google.rpc.BadRequest with one FieldViolation per message, a missing
/// entity is NOT_FOUND. Anything else is an unexpected failure and is masked by the interceptor.
/// </summary>
internal static class GrpcErrors
{
    public static RpcException NotFound(string message) => new(new Grpc.Core.Status(StatusCode.NotFound, message));

    public static async Task ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw InvalidArgument(validation);
        }
    }

    /// <summary>A timestamp outside what <see cref="DateTimeOffset"/> can hold is invalid input, not a server error.</summary>
    public static DateTimeOffset? ToDateTimeOffset(Timestamp? timestamp, string field)
    {
        if (timestamp is null)
        {
            return null;
        }

        try
        {
            return timestamp.ToDateTimeOffset();
        }
        catch (InvalidOperationException)
        {
            throw InvalidArgument([(field, $"{field} must be a valid timestamp.")]);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw InvalidArgument([(field, $"{field} must be a valid timestamp.")]);
        }
    }

    public static RpcException InvalidId() => InvalidArgument([("id", "Id must be a valid UUID.")]);

    public static RpcException InvalidArgument(ValidationResult validation) =>
        InvalidArgument(validation.Errors.Select(error => (ToCamelCase(error.PropertyName), error.ErrorMessage)));

    public static RpcException InvalidArgument(IEnumerable<(string Field, string Description)> violations)
    {
        var badRequest = new BadRequest();
        foreach (var (field, description) in violations.OrderBy(violation => violation.Field, StringComparer.Ordinal))
        {
            badRequest.FieldViolations.Add(new BadRequest.Types.FieldViolation { Field = field, Description = description });
        }

        return new Google.Rpc.Status
        {
            Code = (int)Code.InvalidArgument,
            Message = "The request violates the business rules.",
            Details = { Any.Pack(badRequest) }
        }.ToRpcException();
    }

    private static string ToCamelCase(string property) =>
        property.Length == 0 ? property : char.ToLowerInvariant(property[0]) + property[1..];
}
