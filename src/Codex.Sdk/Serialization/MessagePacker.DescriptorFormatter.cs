using System.Buffers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Codex.ObjectModel;
using Codex.ObjectModel.Attributes;
using Codex.Sdk.Utilities;
using MessagePack;
using MessagePack.Formatters;

namespace Codex.Utilities.Serialization;

public static partial class MessagePacker
{
    private record DescriptorImplFormatter<TImpl, TBase>()
        : DescriptorFormatterBase<TImpl, TBase>(TImpl.GetDescriptor()), IMessagePackFormatter<TImpl>
        where TImpl : TBase, IEntity<TImpl, TBase>
    {
        public void Serialize(ref MessagePackWriter writer, TImpl value, MessagePackSerializerOptions options)
        {
            base.Serialize(ref writer, value, options);
        }
    }

    private record DescriptorBaseFormatter<TImpl, TBase>()
        : DescriptorFormatterBase<TImpl, TBase>(TImpl.GetDescriptor()), IMessagePackFormatter<TBase>
        where TImpl : TBase, IEntity<TImpl, TBase>
    {
        TBase IMessagePackFormatter<TBase>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return base.Deserialize(ref reader, options);
        }
    }

    public static Func<TBase, string, bool> GetShouldSerialize<TImpl, TBase>()
        where TImpl : IShouldSerializeProperty<TBase>
    {
        return TImpl.ShouldSerializeProperty;
    }

    private record DescriptorFormatterBase<TImpl, TBase> :
        IPropertyVisitor<TImpl, TBase>, IJsonFormatterResolver
        where TImpl : TBase, IEntity<TImpl, TBase>
    {
        public DescriptorBase<TImpl, TBase> Descriptor { get; }
        private Dictionary<uint, IFormatProperty> PropertiesByNumber { get; } = new();
        private Dictionary<NameString, IFormatProperty> PropertiesByName { get; set; } = new();
        private List<IFormatProperty> SortedProperties { get; } = new();

        private static Func<TBase, string, bool> CallShouldSerialize;

        static DescriptorFormatterBase()
        {
            if (typeof(TImpl).IsAssignableTo(typeof(IShouldSerializeProperty<TBase>)))
            {
                CallShouldSerialize = TypeSystemHelpers.ReflectionInvoke(ref CallShouldSerialize,
                    () => GetShouldSerialize<Standin, int>(),
                    typeParams: new[] { typeof(TImpl), typeof(TBase) });
            }
        }

        public DescriptorFormatterBase(DescriptorBase<TImpl, TBase> descriptor)
        {
            Descriptor = descriptor;
            descriptor.VisitProperties(this);
        }

        public void Initialize(ObjectStage stage, JsonTypeInfo info)
        {
            // Type info is just used to get full set of properties from inheritance hierarchy
            var propertiesByName = PropertiesByNumber.Values.ToDictionary(p => p.Property.Name, StringComparer.OrdinalIgnoreCase);

            var baseExclusions = typeof(TBase).GetAttributes<ExcludeBaseAttribute>()
                .Where(b => b.ExcludedStages.Contains(stage))
                .Select(b => b.BaseInterface)
                .SelectMany(b => new[] { b }.Concat(b.GetInterfaces()))
                .ToHashSet();

            bool shouldIncludeProperty(JsonPropertyInfo p)
            {
                if (p.AttributeProvider.ShouldRemoveProperty(stage)) return false;

                if (p.AttributeProvider is MemberInfo mi)
                {
                    if (baseExclusions.Contains(mi.DeclaringType))
                    {
                        return false;
                    }
                }

                return true;
            }
            
            PropertiesByName = info.Properties
                .Where(p => propertiesByName.ContainsKey(p.Name))
                .Select(p => propertiesByName[p.Name])
                .ToDictionary(p => GetName(p));
            SortedProperties.AddRange(info.Properties.Where(shouldIncludeProperty)
                .Select(p => propertiesByName.GetOrDefault(p.Name))
                .Where(p => p != null)
                .OrderBy(p => p.Property.Name));
        }

        private NameString GetName(IFormatProperty p)
        {
            return new NameString(p.Property.Name.AsMemory());
        }

        public TImpl Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.NextCode == MessagePackCode.Nil)
            {
                reader.ReadNil();
                return default(TImpl);
            }

            TImpl value = TImpl.Create();
            var count = reader.ReadMapHeader();

            options.Security.DepthStep(ref reader);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var fieldNumber = reader.ReadUInt32();
                    if (PropertiesByNumber.TryGetValue(fieldNumber, out var property))
                    {
                        property.Deserialize(ref reader, value, options);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                value.OnDeserialized();
                return value;
            }
            finally
            {
                reader.Depth--;
            }
        }

        public void Serialize(ref MessagePackWriter writer, TBase value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            if (value is ISerializableEntity serializable)
            {
                serializable.OnSerializing();
            }

            Span<bool> serializedProperties = stackalloc bool[SortedProperties.Count];
            int serializedCount = 0;

            for (int i = 0; i < SortedProperties.Count; i++)
            {
                var property = SortedProperties[i];
                if (serializedProperties[i] = property.ShouldSerialize(value))
                {
                    serializedCount++;
                }
            }

            writer.WriteMapHeader(serializedCount);

            for (int i = 0; i < SortedProperties.Count; i++)
            {
                var property = SortedProperties[i];
                if (serializedProperties[i])
                {
                    writer.Write(property.Property.FieldNumber);
                    property.Serialize(ref writer, value, options);
                }
            }
        }

        public void Visit<TFieldBase, TFieldImpl>(
            DescriptorBase<TImpl, TBase>.Property<TFieldImpl, TFieldBase> property)
            where TFieldImpl : TFieldBase
        {
            PropertiesByNumber.Add(property.FieldNumber, new FormatProperty<TFieldImpl, TFieldBase>(property));
        }

        public void Visit<TFieldBase, TFieldImpl>(
            DescriptorBase<TImpl, TBase>.ListProperty<TFieldImpl, TFieldBase> property)
            where TFieldImpl : TFieldBase
        {
            PropertiesByNumber.Add(property.FieldNumber, new ListFormatProperty<TFieldImpl, TFieldBase>(property));
        }

        public bool IsApplicable()
        {
            return true;
        }

        public IJsonFormatter<T> GetJsonFormatter<T>()
        {
            if (typeof(T) == typeof(TBase))
            {
                return (IJsonFormatter<T>)FuncFormatter.CreateJson(Read<TBase>, Write<TBase>, None.Value);
            }
            else if (typeof(T) == typeof(TImpl))
            {
                return (IJsonFormatter<T>)FuncFormatter.CreateJson(Read<TImpl>, Write<TImpl>, None.Value);
            }

            return null;
        }

        private T Read<T>(ref Utf8JsonReader reader, JsonSerializerOptions options, None data)
            where T : TBase
        {
            var propertyNameBuffer = ArrayPool<char>.Shared.Rent(200);

            try
            {
                TImpl value = TImpl.Create();

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    Contract.Assert(reader.TokenType == JsonTokenType.PropertyName);

                    NameString name = GetName(ref reader, propertyNameBuffer);
                    reader.Read();
                    if (PropertiesByName.TryGetValue(name, out var property))
                    {
                        property.Read(value, ref reader, options);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                value.OnDeserialized();

                return (T)(object)value;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(propertyNameBuffer);
            }
        }

        private NameString GetName(ref Utf8JsonReader reader, char[] nameBuffer)
        {
            var length = reader.CopyString(nameBuffer);
            var nameMemory = nameBuffer.AsMemory(0, length);
            return new NameString(nameMemory);
        }

        private void Write<T>(Utf8JsonWriter writer, T value, bool asPropertyName, JsonSerializerOptions options, None data)
            where T : TBase
        {
            if (value is ISerializableEntity serializable)
            {
                serializable.OnSerializing();
            }

            writer.WriteStartObject();

            for (int i = 0; i < SortedProperties.Count; i++)
            {
                var property = SortedProperties[i];
                if (property.ShouldSerialize(value))
                {
                    writer.WritePropertyName(property.JsonName);
                    property.Write(writer, value, options);
                }
            }

            writer.WriteEndObject();
        }

        private interface IFormatProperty
        {
            DescriptorBase<TImpl, TBase>.IProperty Property { get; }

            void Serialize(ref MessagePackWriter writer, TBase value, MessagePackSerializerOptions options);

            void Deserialize(ref MessagePackReader reader, TImpl value, MessagePackSerializerOptions options);

            bool ShouldSerialize(TBase value);

            JsonEncodedText JsonName { get; }

            void Read(TImpl target, ref Utf8JsonReader reader, JsonSerializerOptions options);

            void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options);
        }

        public record FormatPropertyBase<TFieldImpl, TFieldBase>(
            DescriptorBase<TImpl, TBase>.IProperty<TFieldImpl, TFieldBase> Property)
        {
            private Type ImplConverterType = typeof(TFieldImpl);
            private Type BaseConverterType = typeof(TFieldBase);

            JsonConverter<TFieldImpl> JsonImplConverter;
            JsonConverter<TFieldBase> JsonBaseConverter;

            public Box<JsonEncodedText> _jsonName;
            public JsonEncodedText JsonName => (_jsonName ??= GetJsonName()).Value;

            private JsonEncodedText GetJsonName()
            {
                Span<char> charSpan = stackalloc char[Property.Name.Length];
                Property.Name.CopyTo(charSpan);
                ref char ch = ref charSpan[0];
                ch = IndexingUtilities.ToLowerInvariantFast(ch);

                return JsonEncodedText.Encode(charSpan);
            }

            public void Read(TImpl target, ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var converter = JsonImplConverter ??= (JsonConverter<TFieldImpl>)options.GetConverter(ImplConverterType);
                var value = converter.Read(ref reader, ImplConverterType, options);
                Property.SetImplProperty(target, value);
            }

            public void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
            {
                var converter = JsonBaseConverter ??= (JsonConverter<TFieldBase>)options.GetConverter(BaseConverterType);
                var propertyValue = Property.GetBaseProperty(value);
                converter.Write(writer, propertyValue, options);
            }
        }

        public record FormatProperty<TFieldImpl, TFieldBase>(
            DescriptorBase<TImpl, TBase>.IProperty<TFieldImpl, TFieldBase> Property)
            : FormatPropertyBase<TFieldImpl, TFieldBase>(Property), IFormatProperty
            where TFieldImpl : TFieldBase
        {
            DescriptorBase<TImpl, TBase>.IProperty IFormatProperty.Property => Property;

            public void Deserialize(ref MessagePackReader reader, TImpl value, MessagePackSerializerOptions options)
            {
                var formatter = options.Resolver.GetFormatterWithVerify<TFieldImpl>();
                var readFieldValue = formatter.Deserialize(ref reader, options);
                Property.SetImplProperty(value, readFieldValue);
            }

            public void Serialize(ref MessagePackWriter writer, TBase value, MessagePackSerializerOptions options)
            {
                var formatter = options.Resolver.GetFormatterWithVerify<TFieldBase>();
                var fieldValue = Property.GetBaseProperty(value);
                formatter.Serialize(ref writer, fieldValue, options);
            }

            public bool ShouldSerialize(TBase value)
            {
                var fieldValue = Property.GetBaseProperty(value);
                if (EqualityComparer<TFieldBase>.Default.Equals(fieldValue, default(TFieldBase)))
                {
                    return false;
                }

                if (CallShouldSerialize?.Invoke(value, Property.Name) == false)
                {
                    return false;
                }

                return true;
            }
        }

        public record ListFormatProperty<TFieldImpl, TFieldBase>(
            DescriptorBase<TImpl, TBase>.IProperty<List<TFieldImpl>, IReadOnlyList<TFieldBase>> Property)
            : FormatPropertyBase<List<TFieldImpl>, IReadOnlyList<TFieldBase>>(Property), IFormatProperty
            where TFieldImpl : TFieldBase
        {
            DescriptorBase<TImpl, TBase>.IProperty IFormatProperty.Property => Property;

            public void Deserialize(ref MessagePackReader reader, TImpl value, MessagePackSerializerOptions options)
            {
                var formatter = options.Resolver.GetFormatterWithVerify<List<TFieldImpl>>();
                var list = formatter.Deserialize(ref reader, options);
                Property.SetImplProperty(value, list);
            }

            public void Serialize(ref MessagePackWriter writer, TBase value, MessagePackSerializerOptions options)
            {
                var formatter = options.Resolver.GetFormatterWithVerify<IReadOnlyList<TFieldBase>>();
                var fieldValue = Property.GetBaseProperty(value);
                formatter.Serialize(ref writer, fieldValue, options);
            }

            public bool ShouldSerialize(TBase value)
            {
                var list = Property.GetBaseProperty(value);
                if (list == null || list.Count == 0)
                {
                    return false;
                }

                return true;
            }
        }
    }

}

