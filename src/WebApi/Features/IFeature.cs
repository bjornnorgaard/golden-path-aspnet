namespace WebApi.Features;

/// <summary>
/// Marker contract to keep "feature" types consistent:
/// a request, a result, and a handler.
/// </summary>
public interface IFeature<in TRequest, TResult, in THandler>
{
}