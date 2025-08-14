namespace Codex.Utilities;

public class StringMap<TValue, TCompare> : Dictionary<string, TValue>
    where TCompare : StringCompare.IComparerProvider
{
    public StringMap()
        : base(TCompare.Comparer)
    {
    }
}

public class CaselessStringMap<TValue> : StringMap<TValue, StringCompare.OrdinalIgnoreCase>
{
}

public static class StringCompare
{
    public interface IComparerProvider
    {
        static abstract StringComparer Comparer { get; }
    }

    public class OrdinalIgnoreCase : IComparerProvider
    {
        public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;
    }

    public class Ordinal : IComparerProvider
    {
        public static StringComparer Comparer => StringComparer.Ordinal;
    }
}