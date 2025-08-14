namespace Codex.Utilities
{
    [GeneratorExclude]
    public interface ISelectComparable<T, TCompare> : IComparable<T>
        where T : ISelectComparable<T, TCompare>
    {
        protected TCompare SelectComparable();

        int IComparable<T>.CompareTo(T? other)
        {
            return Comparer<TCompare>.Default.Compare(SelectComparable(), other.SelectComparable());
        }
    }

    [GeneratorExclude]
    public interface IComparableOperators<TSelf, TOther>
        where TSelf : IComparable<TOther>, IComparableOperators<TSelf, TOther>
    {
        static virtual bool operator <(TSelf left, TOther right) => left.CompareTo(right) < 0;

        static virtual bool operator <=(TSelf left, TOther right) => left.CompareTo(right) <= 0;

        static virtual bool operator >(TSelf left, TOther right) => left.CompareTo(right) > 0;

        static virtual bool operator >=(TSelf left, TOther right) => left.CompareTo(right) >= 0;

        static virtual bool operator ==(TSelf left, TOther right) => left.CompareTo(right) == 0;

        static virtual bool operator !=(TSelf left, TOther right) => left.CompareTo(right) != 0;
    }

    public static class Compare
    {
        public static IEqualityComparer<T> ByRef<T>()
            where T : class
        {
            return ReferenceEqualityComparer.Instance;
        }

        public static IEqualityComparer<T> SelectEquality<T, TCompare>(Func<T, TCompare> selector, IEqualityComparer<TCompare> comparer = null)
        {
            return new EqualityComparerBuilder<T>.SelectorEqualityComparer<TCompare>(selector, comparer ?? EqualityComparer<TCompare>.Default);
        }

        public static ComparerBuilder<T> Builder<T>(IEnumerable<T> items = null) => new();

        public static int? DefaultChainCompare<T>(T left, T right)
        {
            var result = Comparer<T>.Default.Compare(left, right);
            return result == 0 ? null : result;
        }
    }
}
