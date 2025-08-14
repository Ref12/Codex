using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Utilities
{
    public record Box<T> : IBox, IBox<T>
    {
        public T Value;

        public Box()
        {
        }

        public Box(T value)
        {
            Value = value;
        }

        public void Invoke<TData>(IAction<TData> action, TData data)
        {
            action.Invoke(data, Value);
        }

        public TResult Invoke<TData, TResult>(IFunc<TData, TResult> func, TData data)
        {
            return func.Invoke(data, Value);
        }

        public TResult Invoke<TData, TResult>(IDerivedFunc<TData, T, TResult> func, TData data)
        {
            return func.Invoke(data, Value);
        }

        public static implicit operator Box<T>(T value)
        {
            return new Box<T>(value);
        }

        public static implicit operator T(Box<T> box) => box.Value;
    }

    public static class Box
    {
        public static Box<T> Create<T>(T value = default)
        {
            return value;
        }

        public static IComparer<Box<T>> CreateComparer<T>(IComparer<T> valueComparer)
        {
            return new ComparerBuilder<Box<T>>().CompareByAfter(b => b.Value, valueComparer);
        }
    }

    public interface IBox<out T>
    {
        TResult Invoke<TData, TResult>(IDerivedFunc<TData, T, TResult> func, TData data);
    }

    public interface IBox
    {
        void Invoke<TData>(IAction<TData> action, TData data);

        TResult Invoke<TData, TResult>(IFunc<TData, TResult> func, TData data);
    }

    public interface IAction<TData>
    {
        void Invoke<TArg>(TData data, TArg arg);
    }

    public interface IDerivedFunc<TData, in TArgBase, TResult>
    {
        TResult Invoke<TArg>(TData data, TArg arg)
            where TArg : TArgBase;
    }

    public interface IFunc<TData, TResult>
    {
        TResult Invoke<TArg>(TData data, TArg arg);
    }

    public interface ILambda<TState, TResult>
    {
        TResult Invoke<T>(TState state);
    }

    public interface IDerivedLambda<TBase, TState, TResult>
    {
        TResult Invoke<T>(TState state)
            where T : class, TBase;
    }
}
