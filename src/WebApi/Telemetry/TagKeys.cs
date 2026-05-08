using Platform.Annotations;
using WebApi.Database.Models;

namespace WebApi.Telemetry;

public static class TelemetryTagKeys
{
    [TagKey(typeof(string), typeof(TodoId))]
    private const string TodoId = "bybear.todo.id";
}