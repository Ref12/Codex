// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Diagnostics.ContractsLight;
using System.Globalization;
using Codex.Utilities.Serialization;

namespace Codex.Configuration
{
    /// <summary>
    /// Defines a setting which can be converted to and from a string. This is used for
    /// to have more convenient json serialization for certain types like enums and TimeSpan
    /// </summary>
    public interface IStringConvertibleSetting
    {
        public string ConvertToString();

        /// <summary>
        /// Convert a string to an instance of the given value.
        /// NOTE: Normally this would be a static method, but for ease of
        /// implementation it is instead defined as an instance member and
        /// the expectation is that the derived type of <see cref="IStringConvertibleSetting"/>
        /// will be a struct which would define this method.
        /// </summary>
        public object ConvertFromString(string value);
    }

    public class StringConvertibleConverter : TypeConverter
    {
        private readonly IStringConvertibleSetting _converter;

        public StringConvertibleConverter(Type type)
        {
            Contract.Requires(type.IsValueType, $"{type} should be a value type");
            Contract.Requires(typeof(IStringConvertibleSetting).IsAssignableFrom(type), $"{type} should be derived from {nameof(IStringConvertibleSetting)}");

            var settingType = typeof(IStringConvertibleSetting);
            var ifaces = type.GetInterfaces()[0];
            bool equal = typeof(IStringConvertibleSetting) == ifaces;



            var instance = Activator.CreateInstance(type);
            _converter = TypeSystemHelpers.ReflectionInvoke<IStringConvertibleSetting>(() => Cast<IStringConvertibleSetting>(default),
                new[] { type },
                instance);
        }

        private static IStringConvertibleSetting Cast<T>(T value)
            where T : IStringConvertibleSetting
        {
            return value;
        }

        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        {
            if (destinationType == typeof(string))
            {
                return true;
            }

            return base.CanConvertTo(context, destinationType);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is not null)
            {
                return ((IStringConvertibleSetting)value).ConvertToString();
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string s)
            {
                return _converter.ConvertFromString(s);
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}