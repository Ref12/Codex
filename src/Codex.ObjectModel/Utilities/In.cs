using System.Runtime.CompilerServices;

namespace Codex.Utilities
{
    public readonly ref struct In<T>
    {
        public bool IsValid { get; }

        public readonly ref T Value => ref _ref;

        private readonly ref T _ref;

        public In(in T value)
        {
            _ref = ref Unsafe.AsRef(value);
            IsValid = true;
        }

        public static implicit operator T(In<T> o)
        {
            return o.Value;
        }
    }

    public static class In
    {
        public static In<T> New<T>(in T value) => new(value);
    }
}