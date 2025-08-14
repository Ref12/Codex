using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public static class Requires
    {
        /// <summary>
        /// Ensure the given value matches the type at compile time. Namely to help
        /// ensure correct data is populated when constructing entity types from other entity types
        /// </summary>
        public static T Expect<T>(this T value)
        {
            return value;
        }

        public static T Cast<T>(object value)
        {
            return (T)value;
        }

        public static Exception UnexpectedEnum<TEnum>(TEnum e)
            where TEnum : struct, Enum
        {
            throw Contract.AssertFailure($"Unexpected enum value: {typeof(TEnum).Name}.{e}");
        }
    }
}
