using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FtxApi
{
    public class FtxStringEnumConverter : StringEnumConverter
    {
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            Enum @enum = (Enum)value;
            writer.WriteRawValue(Enum.GetName(@enum.GetType(), value));
        }
    }
}