using System;
using Newtonsoft.Json;
using UnityEngine;

namespace FollowMePeak.Utils;

public static class CommonJsonSettings
{
    public static readonly JsonSerializerSettings Default = new()
    {
        Formatting = Formatting.Indented,
        Converters = [new ApiVector3Converter()],
    };

    public static readonly JsonSerializerSettings Compact = new()
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        Converters = [new ApiVector3Converter()],
    };

    public class ApiVector3Converter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            Vector3 result = existingValue;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = (string)reader.Value;
                    if (!reader.Read()) continue;

                    switch (propertyName)
                    {
                        case "x":
                        case "X":
                            result.x = Convert.ToSingle(reader.Value);
                            break;
                        case "y":
                        case "Y":
                            result.y = Convert.ToSingle(reader.Value);
                            break;
                        case "z":
                        case "Z":
                            result.z = Convert.ToSingle(reader.Value);
                            break;
                    }
                }
            }

            return result;
        }
    }
}
