using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    public static class IntHelpers
    {
        public static bool TryGetIntegralValue(this IConvertible o, out long value)
        {
            var typeCode = o.GetTypeCode();
            switch (typeCode)
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    value = o.ToInt64(null);
                    return true;
            }

            value = 0;
            return false;
        }

        public static ValueArray<long, (long, long, long)> GetTriangulationValues(this long i)
        {
            const int a = 5;
            const int b = 7;
            const int c = (a * b) / 2;

            var result = new ValueArray<long, (long, long, long)>((
                i / a,
                i / b,
                (i + c) / (a * b)
            ));

            if (result[2] == result[1])
            {
                result.Length--;
            }

            if (result[1] == result[0])
            {
                result.Length--;
            }

            return result;
        }

        public static TEnum Flag<TEnum>(this TEnum e, bool isSet)
            where TEnum : unmanaged, Enum
        {
            return isSet ? e : default;
        }

        public static TEnum Or<TEnum>(this TEnum e, TEnum value)
            where TEnum : unmanaged, Enum
        {
            var result = e.ToInteger();
            result |= value.ToInteger();
            return ToEnum<TEnum>(result);
        }

        public static TTarget As<TSource, TTarget>(TSource source)
        {
            UInt128 value = 0;
            Unsafe.As<UInt128, TSource>(ref value) = source;
            return Unsafe.As<UInt128, TTarget>(ref value);
        }

        public static long ToInteger<TEnum>(this TEnum e)
            where TEnum : unmanaged, Enum
        {
            long result = 0;
            Unsafe.As<long, TEnum>(ref result) = e;
            return result;
        }

        public static TEnum ToEnum<TEnum>(long value)
        {
            return Unsafe.As<long, TEnum>(ref value);
        }

        public static ulong RotateLeft(this ulong original, int bits)
        {
            return (original << bits) | (original >> (64 - bits));
        }

        public static ulong RotateRight(this ulong original, int bits)
        {
            return (original >> bits) | (original << (64 - bits));
        }

        public static uint RotateLeft(this uint original, int bits)
        {
            return (original << bits) | (original >> (32 - bits));
        }

        public static uint RotateRight(this uint original, int bits)
        {
            return (original >> bits) | (original << (32 - bits));
        }

        public static ulong GetUInt64(this ReadOnlySpan<byte> bb, int pos)
        {
            return MemoryMarshal.Read<ulong>(bb.Slice(pos));
        }

        public static int? NonNegativeOrNull(this int i) => i < 0 ? null : i;

        public static bool HasFlag<TInt>(this TInt value, TInt flag)
            where TInt : struct, IBitwiseOperators<TInt, TInt, TInt>, IEqualityOperators<TInt, TInt, bool>
        {
            return (value & flag) == flag;
        }

        public static TInt SetFlag<TInt>(this TInt value, TInt flag)
            where TInt : struct, IBitwiseOperators<TInt, TInt, TInt>, IEqualityOperators<TInt, TInt, bool>
        {
            return (value | flag);
        }

        public static TInt RemoveFlag<TInt>(this TInt value, TInt flag)
            where TInt : struct, IBitwiseOperators<TInt, TInt, TInt>, IEqualityOperators<TInt, TInt, bool>
        {
            return (value & ~flag);
        }

        public static uint CastToUnsigned(this int value)
        {
            return unchecked((uint)value);
        }

        public static int CastToSigned(this uint value)
        {
            return unchecked((int)value);
        }

        public static ulong CastToUnsigned(this long value)
        {
            return unchecked((ulong)value);
        }

        public static long CastToSigned(this ulong value)
        {
            return unchecked((long)value);
        }

        public static bool TryParseInt<TInt>(string str, out TInt parsedInt)
            where TInt : INumberBase<TInt>
        {
            parsedInt = default;
            if (string.IsNullOrEmpty(str)) return false;

            var span = str.AsSpan();
            if (span[0] == '0')
            {
                if (span[1] == 'x')
                {
                    return TInt.TryParse(span.Slice(2), NumberStyles.HexNumber, null, out parsedInt);
                }
                //else if (span[1] == 'b')
                //{
                //    return Int128.TryParse(span.Slice(2), NumberStyles., null, out parsedInt);
                //}
            }

            return TInt.TryParse(span, NumberStyles.AllowLeadingSign, null, out parsedInt);
        }

        public static ValueEnumerator<TInt, TInt> EnumerateSetBits<TInt>(TInt value)
            where TInt : struct, IBinaryInteger<TInt>, IEqualityOperators<TInt, TInt, bool>
        {
            return ValueEnumerator.Create(value, static (ref TInt value, out TInt current) =>
            {
                if (!TInt.IsZero(value))
                {
                    current = TInt.TrailingZeroCount(value);

                    value &= (value - TInt.One);
                    return true;
                }
                else
                {
                    current = default;
                    return false;
                }
            });
        }
    }
}
