using System.Runtime.CompilerServices;
using Codex.Sdk.Utilities;
using CommunityToolkit.HighPerformance;

namespace Codex.Utilities
{
    public readonly ref struct Out<T>
    {
        public bool IsValid { get; }

        public readonly ref T Value => ref Ref.Value;

        private readonly Ref<T> _ref;

        internal Ref<T> Ref
        {
            get
            {
                Contract.Assert(IsValid);
                return _ref;
            }
        }

        public unsafe Out(out T value)
        {
            value = default;
            _ref = new Ref<T>(Unsafe.AsPointer(ref value));
            IsValid = true;
        }

        public Out(ref T value, None _)
        {
            _ref = new Ref<T>(ref value);
            IsValid = true;
        }

        public void Set(T value)
        {
            if (IsValid)
            {
                Ref.Value = value;
            }
        }

        public static implicit operator T(Out<T> o)
        {
            return o.Value;
        }
    }

    public delegate void RefAction<T>(Ref<T> value);
    public delegate void OutAction<T>(Out<T> value);

    public static class Out
    {
        public static Span<T> Span<T>(Span<T> span)
        {
            return span;
        }

        public static T Invoke<T>(Func<T> invoke)
        {
            return invoke();
        }

        public static void Invoke(Action invoke)
        {
            invoke();
        }

        public static T ApplyTo<T>(this RefAction<T>? refAction, T value)
        {
            if (refAction != null)
            {
                refAction(CreateRef(ref value));
            }

            return value;
        }

        public static Action<T> Action<T>(Action<T> action) => action;

        public static Ref<T> CreateRef<T>(ref T value) => new Ref<T>(ref value);

        public static Out<T> Create<T>(ref T value) => new Out<T>(ref value, default);

        public static void SetOrCreate<T>(this ref Out<T> box, in T value)
        {
            if (box.IsValid)
            {
                box.Set(value);
            }
            else
            {
                box = Create(ref Unsafe.AsRef(value));
            }
        }

        public static void Ensure<T>(this ref Out<T> box, in T value = default)
        {
            if (!box.IsValid)
            {
                box = Create(ref Unsafe.AsRef(value));
            }
        }

        public static Ref<T> Ref<T>(out T value) => new Out<T>(out value).Ref;

        public static ref T VarRef<T>(out T local)
        {
            local = default;
            return ref Unsafe.AsRef(local);
        }

        public static ref T RefValue<T>(out T local, T value)
        {
            local = value;
            return ref Unsafe.AsRef(local);
        }

        public static ref T RefReturn<T>(ref T value) => ref value;

        public static bool TryBoth(bool left, bool right)
        {
            return left || right;
        }

        public static bool VarIf<T>(bool condition, in T input, out T value)
        {
            value = condition ? input : default;
            return condition;
        }

        public static T Return<T, T2>(T value, T2 _)
        {
            return value;
        }

        public static T Var<T>(out T value, T input)
        {
            value = input;
            return value;
        }

        public static void Create<T>(out T primary, out T secondary, Func<T> factory)
        {
            primary = factory();
            secondary = factory();
        }

        public static void Swap<T>(ref T v1, ref T v2)
        {
            var temp = v1;
            v1 = v2;
            v2 = temp;
        }

        public static T MaybeVar<T>(out T? value, T input)
            where T : struct
        {
            value = input;
            return input;
        }

        public static bool TrueVar<T>(out T value, T input)
        {
            value = input;
            return true;
        }

        public static bool TryVar<T>(bool condition, out T value, T input)
        {
            value = input;
            return condition;
        }
    }
}