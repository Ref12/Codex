namespace Codex.Utilities
{
    public record struct ReferenceEquatable<T>(T Value) : IEquatable<ReferenceEquatable<T>>
    {
        public bool Equals(ReferenceEquatable<T> other)
        {
            return ReferenceEqualityComparer.Instance.Equals(Value, other.Value);
        }

        public override int GetHashCode()
        {
            return ReferenceEqualityComparer.Instance.GetHashCode(Value);
        }

        public static implicit operator T(ReferenceEquatable<T> r) => r.Value;

        public static implicit operator ReferenceEquatable<T>(T value) => new(value);
    }

    public record ReferenceEquatableBase
    {
        public virtual bool Equals(ReferenceEquatableBase other)
        {
            return ReferenceEqualityComparer.Instance.Equals(this, other);
        }

        public override int GetHashCode()
        {
            return ReferenceEqualityComparer.Instance.GetHashCode(this);
        }
    }
}
