namespace Codex.Utilities
{
    /// <summary>
    /// Used to specify out paramater to async method.
    /// </summary>
    public record struct SubRef<T>
    {
        /// <summary>
        /// The value
        /// </summary>
        public T Value;

        public SubRef()
        {
        }

        public SubRef(out SubRef<T> value)
        {
            value = this;
        }

        /// <nodoc />
        public static implicit operator T(SubRef<T> value)
        {
            return value.Value;
        }

        public void Set(T value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// Helper methods for <see cref="SubRef{T}"/>
    /// </summary>
    public static class SubRef
    {
        public static SubRef<T> SetOrCreate<T>(this ref SubRef<T>? box, T value = default)
        {
            box ??= new SubRef<T>();
            box.Value.Set(value);
            return box.Value;
        }

        /// <summary>
        /// Allows inline declaration of <see cref="SubRef{T}"/> patterns like the
        /// (out T parameter) pattern. Usage: await ExecuteAsync(out SubRef.Var&lt;T&gt;(out var outParam));
        /// </summary>
        public static SubRef<T> Var<T>(out SubRef<T> value)
        {
            value = new SubRef<T>();
            return value;
        }
    }
}