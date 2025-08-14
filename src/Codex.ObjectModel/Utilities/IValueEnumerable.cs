namespace Codex.Utilities
{
    public interface IValueEnumerable<TSelf, T> : IStandardEnumerable<T>
        where TSelf : IValueEnumerable<TSelf, T>
    {
        new ValueEnumerator<TSelf, T> GetEnumerator();

        static abstract bool TryMoveNext(ref TSelf state, out T current);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    }

    public interface IValueEnumerable<TSelf, TState, T> : IStandardEnumerable<T>
        where TSelf : IValueEnumerable<TSelf, TState, T>
    {
        new ValueEnumerator<(TSelf Self, TState State), T> GetEnumerator() => new ValueEnumerator<(TSelf Self, TState State), T>(default, TSelf.TryMoveNext);

        static abstract bool TryMoveNext(ref (TSelf Self, TState State) state, out T current);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    }
}
