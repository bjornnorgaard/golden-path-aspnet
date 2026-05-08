using Platform.Annotations;
using WebApi.Database.Models;

// ReSharper disable UnusedMember.Local
// The private constant TodoId appears unused but is actually referenced by the TagKey attribute
// for telemetry tag registration, through source generation.

// ReSharper disable UnusedType.Global
// The TelemetryTagKeys class appears unused but is actually referenced through source generation
// for telemetry tag registration via the TagKey attributes on its members.

namespace WebApi.Telemetry;

/// <summary>
/// Provides a centralized collection of keys used for telemetry tagging.
/// This static class is intended to define constant values that represent
/// the telemetry tags used throughout the application.
/// </summary>
public static class TelemetryTagKeys
{
    [TagKey(typeof(string), typeof(TodoId))]
    private const string TodoId = "bybear.todo.id";
}