using Codex.Utilities.Serialization;

namespace Codex.Utilities;

public abstract record TextSourceBase : IJsonConvertible<TextSourceBase, string>
{
    public abstract string GetString();

    //IReadOnlyList<ReadOnlyMemory<char>> GetLines();

    public abstract TextReader GetReader();

    public virtual ITextSourceReader GetSourceReader()
    {
        return new TextSourceReader(GetReader());
    }

    public override string ToString()
    {
        return GetString();
    }

    public string AsString => GetString();

    public static TextSourceBase ConvertFromJson(string jsonFormat)
    {
        return jsonFormat;
    }

    public string ConvertToJson()
    {
        return GetString();
    }

    public static implicit operator TextSourceBase(string value)
    {
        if (value == null) return null;
        return new StringTextSource(value);
    }
}

public record StringTextSource(string Value) : TextSourceBase, IJsonConvertible<StringTextSource, string>
{
    static Type IJsonConvertible.JsonFormatType => typeof(string);

    static StringTextSource IJsonConvertible<StringTextSource, string>.ConvertFromJson(string jsonFormat)
    {
        return new(jsonFormat);
    }

    public override TextReader GetReader()
    {
        return new StringReader(Value);
    }

    public override string GetString()
    {
        return Value;
    }
}

public record StreamTextSource(Func<Stream> GetStream)
    : ReaderTextSource(() => new StreamReader(GetStream()))
{
}

public record ReaderTextSource(Func<TextReader> GetReaderValue) : TextSourceBase
{
    public override TextReader GetReader()
    {
        return GetReaderValue();
    }

    public override ITextSourceReader GetSourceReader()
    {
        var reader = base.GetSourceReader();
        return reader.TextReader as ITextSourceReader ?? reader;
    }

    public override string GetString()
    {
        return GetReader().ReadToEnd();
    }
}

public record TextSourceReader(TextReader TextReader) : ITextSourceReader
{
    public void Dispose()
    {
        TextReader.Dispose();
    }

    public ReadOnlySpan<char> TryRead(int length, Span<char> buffer)
    {
        if (buffer.Length == 0)
        {
            buffer = new char[length];
        }
        else if (buffer.Length > length)
        {
            buffer = buffer.Slice(0, length);
        }

        int read = TextReader.Read(buffer);
        return buffer.Slice(0, read);
    }
}

[GeneratorExclude]
public interface ITextSourceReader : IDisposable
{
    TextReader TextReader { get; }

    ReadOnlySpan<char> TryRead(int length, Span<char> buffer);
}

public class CompositeTextReader
    : TextReader, ITextSourceReader
{
    public CompositeTextReader(IReadOnlyList<CharString> lines)
    {
        this.Lines = lines;
        this.iterator = lines.GetIterator();
    }

    public IReadOnlyList<CharString> Lines { get; }

    public TextReader TextReader => this;

    private int _offset = 0;
    private readonly IIterator<CharString> iterator;

    private bool TryGetCurrent(out ReadOnlySpan<char> chars)
    {
        while (iterator.TryGetCurrent(out var s))
        {
            if (s.Length == _offset)
            {
                iterator.MoveNext();
                _offset = 0;
                continue;
            }

            chars = s.Span;
            return true;
        }

        chars = default;
        return false;
    }

    public override int Peek()
    {
        if (TryGetCurrent(out var s) && Extent.IsValidIndex(_offset, s.Length))
        {
            return s[_offset];
        }

        return -1;
    }

    public override int Read()
    {
        if (TryGetCurrent(out var s) && Extent.IsValidIndex(_offset, s.Length))
        {
            return s[_offset++];
        }

        return -1;
    }

    public override int Read(char[] buffer, int index, int count)
    {
        return Read(buffer.AsSpan(index, count));
    }

    public override int Read(Span<char> buffer)
    {
        int readChars = 0;
        var originalBuffer = buffer;
        while (buffer.Length > 0 && TryGetCurrent(out var s))
        {
            var remainingChars = s.Slice(_offset);
            var adjustedCount = (int)Math.Min(buffer.Length, remainingChars.Length);

            remainingChars.Slice(0, adjustedCount).CopyTo(buffer.Slice(0, adjustedCount));

            readChars += adjustedCount;
            buffer = buffer.Slice(adjustedCount);

            _offset += adjustedCount;
        }

        return readChars;
    }

    public ReadOnlySpan<char> TryRead(int length, Span<char> buffer)
    {
        if (TryGetCurrent(out var chars))
        {
            chars = chars.Slice(_offset);
            if (chars.Length >= length)
            {
                chars = chars.Slice(0, length);
            }

            _offset += chars.Length;
            return chars;
        }

        return ReadOnlySpan<char>.Empty;
    }
}