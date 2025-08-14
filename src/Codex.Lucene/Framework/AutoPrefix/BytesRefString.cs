using System.Numerics;
using Codex.Utilities;
using Lucene.Net.Queries.Function.ValueSources;
using Lucene.Net.Util;

namespace Codex.Lucene.Framework.AutoPrefix
{
    public readonly struct BytesRefString : IEquatable<BytesRefString>, IComparable<BytesRefString>, IComparable<BytesRef>,
        IComparisonOperators<BytesRefString, BytesRefString, bool>,
        IComparisonOperators<BytesRefString, BytesRef, bool>
    {
        public BytesRef Value { get; }

        public int Length => Value.Length;

        public byte[] Bytes => Value.Bytes;

        public byte this[int i] => Bytes[i + Value.Offset];

        public Span<byte> Span => Value != null ? Value.Span : default;

        public BytesRefString(BytesRef value)
        {
            Value = value;
        }

        public BytesRefString Copy() => new(BytesRef.DeepCopyOf(Value));

        public static implicit operator BytesRefString(BytesRef value)
        {
            return new BytesRefString(value);
        }

        public static implicit operator BytesRefString(string value)
        {
            return new BytesRefString(new BytesRef(value));
        }

        public static implicit operator BytesRef(BytesRefString value)
        {
            return value.Value;
        }

        public string GetString() => Value?.Utf8ToString();

        public override string ToString()
        {
            return $"{Value?.Utf8ToString()} [{Value?.Length ?? -1}]";
        }

        public bool Equals(BytesRefString other)
        {
            return Span.SequenceEqual(other.Span);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public int CompareTo(BytesRefString other)
        {
            return Value.CompareTo(other.Value);
        }

        public int CompareTo(BytesRef? other)
        {
            return Value.CompareTo(other);
        }

        public static bool operator ==(BytesRefString left, BytesRefString right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BytesRefString left, BytesRefString right)
        {
            return !(left == right);
        }

        public static bool operator <(BytesRefString left, BytesRefString right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(BytesRefString left, BytesRefString right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(BytesRefString left, BytesRefString right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(BytesRefString left, BytesRefString right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator <(BytesRefString left, BytesRef right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(BytesRefString left, BytesRef right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(BytesRefString left, BytesRef right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(BytesRefString left, BytesRef right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(BytesRefString left, BytesRef? right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BytesRefString left, BytesRef? right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return obj is BytesRefString && Equals((BytesRefString)obj);
        }
    }
}
