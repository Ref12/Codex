// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.ContractsLight;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Codex.Utilities;

[StructLayout(LayoutKind.Explicit, Size = 3)]
public struct UInt24 : IAdditionOperators<UInt24, int, UInt24>, IComparable<UInt24>
{
    public static UInt24 Zero { get; } = default;

    public static UInt24 One { get; } = (UInt24)1;

    public static UInt24 Max { get; } = (UInt24)((1 << 24) - 1);

    public static UInt24 Min { get; } = Zero;

    [FieldOffset(2)]
    private byte high;

    [FieldOffset(0)]
    private ushort low;

    public int GetValue() => To<int>();

    public static implicit operator int(UInt24 value) => value.GetValue();

    public static implicit operator long(UInt24 value) => value.GetValue();

    public static implicit operator ulong(UInt24 value) => value.To<ulong>();

    public static implicit operator uint(UInt24 value) => value.To<uint>();

    public static implicit operator UInt24(int value)
    {
        return From(value);
    }

    public static explicit operator UInt24(long value)
    {
        return From(value);
    }

    public static explicit operator UInt24(ulong value)
    {
        return From(value);
    }

    public static explicit operator UInt24(uint value)
    {
        return From(value);
    }

    public static explicit operator UInt24(short value)
    {
        return From(value);
    }

    public static implicit operator UInt24(ushort value)
    {
        return From(value);
    }

    public static implicit operator UInt24(byte value)
    {
        return From(value);
    }

    public static UInt24 operator +(UInt24 left, int right)
    {
        return From(left.GetValue() + right);
    }

    public static UInt24 operator ++(UInt24 left)
    {
        return left + 1;
    }

    public static UInt24 From<TInt>(TInt value)
        where TInt : unmanaged, INumber<TInt>
    {
        return IntHelpers.As<TInt, UInt24>(value);
    }

    public TInt To<TInt>()
        where TInt : unmanaged, INumber<TInt>
    {
        return IntHelpers.As<UInt24, TInt>(this);
    }

    public override string ToString()
    {
        return GetValue().ToString();
    }

    public override int GetHashCode()
    {
        var code = GetValue().GetHashCode();
        return code;
    }

    public int CompareTo(UInt24 other)
    {
        return GetValue().CompareTo(other.GetValue());
    }
}
