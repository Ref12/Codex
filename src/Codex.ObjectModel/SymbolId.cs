using System.Numerics;

namespace Codex.ObjectModel
{
    public partial struct SymbolId : IEquatable<SymbolId>, IEqualityOperators<SymbolId, SymbolId, bool>
    {
        public readonly string Value;

        public string DebugId;

        private SymbolId(string value, bool ignored)
        {
            Value = value;
        }

        public static SymbolId UnsafeCreateWithValue(string value)
        {
            if (value?.StartsWith("#") == true)
            {
                return SymbolId.CreateFromId(value.AsSpan(1), value);
            }

            return new SymbolId(value, true);
        }

        public bool Equals(SymbolId other)
        {
            return Value == other.Value;
        }

        public override string ToString()
        {
            return Value;
        }

        public bool IsValid => Value != null;

        public static SymbolId CreateFromId(string id)
        {
            // return new SymbolId(id);
            return CreateFromId(id.AsSpan(), id);
        }

        public static SymbolId CreateFromId(ReadOnlySpan<char> id, string debugId)
        {
            // return new SymbolId(id);
            return new SymbolId(IndexingUtilities.ComputeSymbolUid(id), true)
            {
                DebugId = debugId
            };
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value);
        }

        public static bool operator ==(SymbolId left, SymbolId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SymbolId left, SymbolId right)
        {
            return !left.Equals(right);
        }

        public bool TryGetBinaryValue(out long value)
        {
            return TryGetBinaryValue(Value, out value);
        }

        public static bool TryGetBinaryValue(string str, out long value)
        {
            value = 0;
            if (str?.Length != IndexingUtilities.UidLength) return false;

            for (int i = 0; i < str.Length; i++)
            {
                value *= 36;

                var c = str[i];
                if (char.IsBetween(c, '0', '9'))
                {
                    value += c - '0';
                }
                else if (char.IsBetween(c, 'a', 'z'))
                {
                    value += (c - 'a') + 10;
                }
                else if (char.IsBetween(c, 'A', 'Z'))
                {
                    value += (c - 'A') + 10;
                }
                else
                {
                    // Not alphanumeric
                    return false;
                }
            }

            return true;
        }
    }
}
