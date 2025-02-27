using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRD.Controllers
{
    public class EnumMemberJsonConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeof(EnumMemberConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType);
        }

        private class EnumMemberConverter<T> : JsonConverter<T> where T : struct, Enum
        {
            private readonly Dictionary<T, string> _enumToString = new Dictionary<T, string>();
            private readonly Dictionary<string, T> _stringToEnum = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

            public EnumMemberConverter()
            {
                var type = typeof(T);
                var values = Enum.GetValues(type).Cast<T>();

                foreach (var value in values)
                {
                    var enumMember = type.GetMember(value.ToString())[0]
                        .GetCustomAttribute<EnumMemberAttribute>();

                    var stringValue = enumMember?.Value ?? value.ToString();
                    _enumToString[value] = stringValue;
                    _stringToEnum[stringValue] = value;
                }
            }

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                    return default;

                var stringValue = reader.GetString();
                if (string.IsNullOrEmpty(stringValue) || !_stringToEnum.TryGetValue(stringValue, out var enumValue))
                {
                    return default;
                }

                return enumValue;
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                if (_enumToString.TryGetValue(value, out var stringValue))
                {
                    writer.WriteStringValue(stringValue);
                }
                else
                {
                    writer.WriteStringValue(value.ToString());
                }
            }
        }
    }
}