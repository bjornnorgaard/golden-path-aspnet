namespace WebApi.Features;

/// <summary>
/// Marker contract to keep "feature" types consistent:
/// endpoint DTOs + handler types + required mapping.
/// </summary>
public interface IFeature<TRequestBody, TResponseBody, TCommand, TResult, THandler>
{
    static abstract TCommand MapToCommand(TRequestBody request);
    static abstract TResponseBody MapToResponseBody(TResult result);
}