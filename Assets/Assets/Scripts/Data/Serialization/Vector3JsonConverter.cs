using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(value.x);
        writer.WritePropertyName("y"); writer.WriteValue(value.y);
        writer.WritePropertyName("z"); writer.WriteValue(value.z);
        writer.WriteEndObject();
    }

    public override Vector3 ReadJson(JsonReader reader, System.Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return Vector3.zero;

        var obj = JObject.Load(reader);
        return new Vector3(
            (float)(obj["x"] ?? 0f),
            (float)(obj["y"] ?? 0f),
            (float)(obj["z"] ?? 0f)
        );
    }
}

public sealed class Vector2JsonConverter : JsonConverter<Vector2>
{
    public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(value.x);
        writer.WritePropertyName("y"); writer.WriteValue(value.y);
        writer.WriteEndObject();
    }

    public override Vector2 ReadJson(JsonReader reader, System.Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return Vector2.zero;

        var obj = JObject.Load(reader);
        return new Vector2(
            (float)(obj["x"] ?? 0f),
            (float)(obj["y"] ?? 0f)
        );
    }
}

public sealed class QuaternionJsonConverter : JsonConverter<Quaternion>
{
    public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(value.x);
        writer.WritePropertyName("y"); writer.WriteValue(value.y);
        writer.WritePropertyName("z"); writer.WriteValue(value.z);
        writer.WritePropertyName("w"); writer.WriteValue(value.w);
        writer.WriteEndObject();
    }

    public override Quaternion ReadJson(JsonReader reader, System.Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return Quaternion.identity;

        var obj = JObject.Load(reader);
        return new Quaternion(
            (float)(obj["x"] ?? 0f),
            (float)(obj["y"] ?? 0f),
            (float)(obj["z"] ?? 0f),
            (float)(obj["w"] ?? 1f)
        );
    }
}
