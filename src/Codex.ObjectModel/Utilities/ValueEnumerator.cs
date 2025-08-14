using System.Collections;

namespace Codex.Utilities
{
    public static class ValueEnumerator
    {
        public static ValueEnumerator<TState, T> Create<TState, T>(TState state, TryMoveNext<TState, T> tryMoveNext)
        {
            return new(state, tryMoveNext);
        }

        public static ValueEnumerator<TState, T> CreateEnumerator<TState, T>(this ref TState state, T _ = default)
            where TState : struct, IValueEnumerable<TState, T>
        {
            return new(state, TState.TryMoveNext);
        }
    }

    public delegate bool TryMoveNext<TState, T>(ref TState state, out T current);

    public struct ValueEnumerator<TState, T> : IEnumerator<T>, IValueEnumerable<ValueEnumerator<TState, T>, T>
    {
        private readonly TryMoveNext<TState, T> _tryMoveNext;
        private TState _state;
        private T _current;

        public T Current => _current;

        object IEnumerator.Current => Current;

        public ValueEnumerator(TState state, TryMoveNext<TState, T> tryMoveNext)
        {
            _state = state;
            _tryMoveNext = tryMoveNext;
            _current = default;
        }

        public bool TryMoveNext(out T current)
        {
            bool result = _tryMoveNext(ref _state, out _current);
            current = _current;
            return result;
        }

        public bool MoveNext()
        {
            return _tryMoveNext(ref _state, out _current);
        }

        public void Dispose()
        {
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public ValueEnumerator<ValueEnumerator<TState, T>, T> GetEnumerator()
        {
            return new(this, TryMoveNext);
        }

        public static bool TryMoveNext(ref ValueEnumerator<TState, T> state, out T current)
        {
            return state.TryMoveNext(out current);
        }
    }
}
