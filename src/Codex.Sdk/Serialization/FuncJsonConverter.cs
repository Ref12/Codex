using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codex.Utilities;

/// <summary>
/// Creates json converters from delegates
/// </summary>
public static class FuncJsonConverter
{
    /// <summary>
    /// Creates a json converter from the given delegates
    /// </summary>
    public static JsonConverter<T> Create<T>(
        JsonReaderFunc<T> read,
        Action<Utf8JsonWriter, T> write)
    {
        return new Converter<T>(read, (writer, value, isName) =>
        {
            write(writer, value);
        }, supportPropertyName: false);
    }

    /// <summary>
    /// Creates a json converter from the given delegates
    /// </summary>
    public static JsonConverter<T> CreateString<T>(
        Func<string, T> read,
        Func<T, string> write)
    {
        return new Converter<T>(
            (ref Utf8JsonReader reader) =>
            {
                return read(reader.GetString());
            },
            (writer, value, isName) =>
            {
                var stringValue = write(value);
                if (isName)
                {
                    writer.WritePropertyName(stringValue);
                }
                else
                {
                    writer.WriteStringValue(stringValue);
                }
            }, 
            supportPropertyName: true);
    }

    /// <nodoc />
    public delegate T JsonReaderFunc<T>(ref Utf8JsonReader reader);

    private delegate void JsonWriterFunc<T>(Utf8JsonWriter writer, T value, bool isName);

    private class Converter<T> : JsonConverter<T>
    {
        private readonly JsonReaderFunc<T> ReadValue;
        private readonly JsonWriterFunc<T> WriteValue;
        public bool SupportPropertyName { get; }

        public Converter(JsonReaderFunc<T> read, JsonWriterFunc<T> write, bool supportPropertyName)
        {
            ReadValue = read;
            WriteValue = write;
            SupportPropertyName = supportPropertyName;
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return ReadValue(ref reader);
        }

        public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (SupportPropertyName)
            {
                return ReadValue(ref reader);
            }
            else
            {
                return base.ReadAsPropertyName(ref reader, typeToConvert, options);
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            WriteValue(writer, value, isName: false);
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (SupportPropertyName)
            {
                WriteValue(writer, value, isName: true);
            }
            else
            {
                base.WriteAsPropertyName(writer, value, options);
            }
        }
    }
}
