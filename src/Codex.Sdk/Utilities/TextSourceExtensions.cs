namespace Codex.Utilities;

public static class TextSourceExtensions
{
    private static readonly char[] SkipBuffer = new char[1 << 12];

    private static ReadOnlySpan<char> Var(out ReadOnlySpan<char> local, ReadOnlySpan<char> value)
    {
        return local = value;
    }

    public static void SkipRequired(this ITextSourceReader reader, int length)
    {
        int remaining = length;

        while (Var(out var chars, reader.TryRead(remaining, SkipBuffer)).Length > 0)
        {
            Contract.Assert(chars.Length <= remaining);
            remaining -= chars.Length;
        }

        Contract.Check(remaining == 0)?.Assert($"Skipped {length - remaining} of required {length} characters.");

    }

    public static ReadOnlySpan<char> ReadRequired(this ITextSourceReader reader, int length, Span<char> buffer = default)
    {
        var remaining = length;
        if (Var(out var chars, reader.TryRead(remaining, buffer)).Length > 0)
        {
            Contract.Assert(chars.Length <= remaining);
            remaining -= chars.Length;
            if (remaining == 0) return chars;

            if (buffer.Length < length)
            {
                buffer = new char[length];
            }
            else
            {
                buffer = buffer.Slice(0, length);
            }

            var resultBuffer = buffer;
            chars.CopyTo(buffer);
            buffer = buffer.Slice(chars.Length);

            while (Var(out chars, reader.TryRead(remaining, buffer)).Length > 0)
            {
                Contract.Assert(chars.Length <= remaining);
                remaining -= chars.Length;
                buffer = buffer.Slice(chars.Length);

                if (remaining == 0) return resultBuffer;
            }
        }

        Contract.Check(length == 0)?.Assert($"Read {length - remaining} of required {length} characters.");
        return ReadOnlySpan<char>.Empty;
    }

    public static void ReadRequired<TArg>(this ITextSourceReader reader, int length, TArg arg, ReadOnlySpanAction<char, TArg> handle, Span<char> buffer = default)
    {
        var remaining = length;
        while (remaining > 0 && Var(out var chars, reader.TryRead(remaining, buffer)).Length > 0)
        {
            Contract.Assert(chars.Length <= remaining);
            remaining -= chars.Length;
            handle(chars, arg);
        }

        Contract.Check(remaining == 0)?.Assert($"Read {length - remaining} of required {length} characters.");
    }

    public static void ReadRemaing<TArg>(this ITextSourceReader reader, TArg arg, ReadOnlySpanAction<char, TArg> handle, Span<char> buffer)
    {
        while (Var(out var chars, reader.TryRead(buffer.Length, buffer)).Length > 0)
        {
            handle(chars, arg);
        }
    }

    public static ReadHandler<TArg> CreateHandler<TArg>(this ITextSourceReader reader, TArg arg, ReadOnlySpanAction<char, TArg> handle, Span<char> buffer)
    {
        return new(reader, arg, handle, buffer);
    }

    public ref struct ReadHandler<TArg>(ITextSourceReader reader, TArg arg, ReadOnlySpanAction<char, TArg> handle, Span<char> buffer)
    {
        public TArg Data = arg;

        public Span<char> Buffer = buffer;

        public void ReadRemaining()
        {
            reader.ReadRemaing(Data, handle, Buffer);
        }

        public void ReadRequired(int length)
        {
            reader.ReadRequired(length, Data, handle, Buffer);
        }
    }
}
