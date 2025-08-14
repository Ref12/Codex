namespace Codex.Sdk.Utilities
{
    public interface ITypeBox
    {
        TResult Invoke<TState, TResult>(ILambda<TState, TResult> func, TState state) => default;
    }

    public interface ITypeBox<T> : ITypeBox
    {
        TResult ITypeBox.Invoke<TState, TResult>(ILambda<TState, TResult> func, TState state)
        {
            return func.Invoke<T>(state);
        }
    }

    public interface IDerivedTypeBox<TBase>
    {
        TResult Invoke<TState, TResult>(IDerivedLambda<TBase, TState, TResult> func, TState state) => default;
    }

    public interface IDerivedTypeBox<T, TBase> : IDerivedTypeBox<TBase>
        where T : class, TBase
    {
        TResult IDerivedTypeBox<TBase>.Invoke<TState, TResult>(IDerivedLambda<TBase, TState, TResult> func, TState state)
        {
            return func.Invoke<T>(state);
        }
    }

    public static class TypeBox
    {
        public static ITypeBox<T> Get<T>() => TypeBox<T>.Instance;

        public static TResult? As<T, TResult>(this T value, ITypeBox<TResult> resultType)
            where TResult : class, T
        {
            return value as TResult;
        }

        public static ITypeBox<T> ItemType<T>(this IEnumerable<T> items)
        {
            return Get<T>();
        }

        public static IDerivedTypeBox<TBase> GetDerived<T, TBase>()
            where T : class, TBase
        {
            return DerivedTypeBox<T, TBase>.Instance;
        }

        public static TResult Invoke<TBase, TState, TResult>(this IDerivedLambda<TBase, TState, TResult> func, IDerivedTypeBox<TBase> typeArg, TState state)
        {
            return typeArg.Invoke(func, state);
        }
    }

    public class TypeBox<T> : ITypeBox<T>
    {
        public static readonly TypeBox<T> Instance = new();
    }

    public class DerivedTypeBox<T, TBase> : IDerivedTypeBox<T, TBase>, ITypeBox<T>
        where T : class, TBase
    {
        public static readonly DerivedTypeBox<T, TBase> Instance = new();
    }
}
