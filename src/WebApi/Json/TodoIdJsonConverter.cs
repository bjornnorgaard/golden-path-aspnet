using System.Text.Json;
using System.Text.Json.Serialization;
using WebApi.Database.Models;

namespace WebApi.Json;

public sealed class TodoIdJsonConverter : JsonConverter<TodoId>
{
    public override TodoId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (!TodoId.TryParse(s, out var id))
        {
            throw new JsonException("Invalid TodoId.");
        }

        return id;
    }

    public override void Write(Utf8JsonWriter writer, TodoId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value.ToString());
    }
}