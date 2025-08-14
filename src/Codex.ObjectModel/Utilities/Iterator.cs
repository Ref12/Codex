namespace Codex.Utilities
{
    public record struct Iterator<TEnumerator, T>(TEnumerator Enumerator, bool moveNext = true) : IIterator<T>
        where TEnumerator : IEnumerator<T>
    {
        public bool IsValid { get; private set; } = moveNext && Enumerator.MoveNext();

        public int Index { get; private set; } = moveNext ? 1 : 0;

        public T Value => IsValid ? Enumerator.Current : default;

        public void MoveTo<TData>(TData data, Func<T, TData, bool> moveNext)
        {
            while (IsValid && moveNext(Value, data))
            {
                IsValid = TryMoveNext(out _);
            }
        }

        public bool TryGetCurrent(out T value)
        {
            value = Value;
            return IsValid;
        }

        public bool TryMoveNext(out T value)
        {
            if (IsValid = Enumerator.TryMoveNext(out value))
            {
                Index++;
                return true;
            }

            return false;
        }

        public bool MoveNext()
        {
            return TryMoveNext(out _);
        }
    }

    public interface IIterator<T>
    {
        bool IsValid { get; }

        int Index { get; }

        T Value { get; }

        void MoveTo<TData>(TData data, Func<T, TData, bool> moveNext);

        bool TryMoveNext(out T value);

        bool MoveNext();

        bool TryGetCurrent(out T value);
    }
}
