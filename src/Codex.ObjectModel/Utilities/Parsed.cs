using System.Numerics;

namespace Codex.Utilities
{
    public record struct Parsed<T>(T Value)
        where T : ISpanParsableSlim<T>
    {
        public static implicit operator Parsed<T>(string value) => new(T.Parse(value));

        public static implicit operator Parsed<T>(T value) => new(value);

        public static implicit operator T(Parsed<T> value) => value.Value;
    }

    public interface IParsableSlim<TSelf> : IParsable<TSelf>
        where TSelf : IParsableSlim<TSelf>
    {
        abstract static TSelf Parse(string value);

        static TSelf IParsable<TSelf>.Parse(string s, System.IFormatProvider? provider)
        {
            return TSelf.Parse(s);
        }

        static bool IParsable<TSelf>.TryParse(string? s, System.IFormatProvider? provider, out TSelf result)
        {
            result = TSelf.Parse(s);
            return true;
        }
    }

    public interface ISpanParsableSlim<TSelf>// : ISpanParsable<TSelf>
        where TSelf : ISpanParsableSlim<TSelf>
    {
        abstract static TSelf Parse(ReadOnlySpan<char> value);

        //static TSelf IParsable<TSelf>.Parse(string s, System.IFormatProvider? provider)
        //{
        //    return TSelf.Parse(s);
        //}

        //static bool IParsable<TSelf>.TryParse(string? s, System.IFormatProvider? provider, out TSelf result)
        //{
        //    result = TSelf.Parse(s);
        //    return true;
        //}

        //static TSelf ISpanParsable<TSelf>.Parse(ReadOnlySpan<char> s, System.IFormatProvider? provider)
        //{
        //    return TSelf.Parse(s);
        //}

        //static bool ISpanParsable<TSelf>.TryParse(ReadOnlySpan<char> s, System.IFormatProvider? provider, out TSelf result)
        //{
        //    result = TSelf.Parse(s);
        //    return true;
        //}
    }
}
