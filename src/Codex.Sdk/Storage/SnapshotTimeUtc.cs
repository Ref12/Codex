using Codex.Utilities.Serialization;

namespace Codex.Storage;

public readonly record struct SnapshotTimeUtc(long Ticks) : IJsonConvertible<SnapshotTimeUtc, DateTimeOffset>, ISelectComparable<SnapshotTimeUtc, long>
{
    public static readonly SnapshotTimeUtc Invalid = new(0);

    public bool IsValid => Ticks > 0;

    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
    private const string PathFormat = "yyyy-MM-ddTHHmmss.fffffffZ";
    public const string QueryKey = "snapshot";

    public static implicit operator DateTimeOffset(SnapshotTimeUtc time) => new DateTimeOffset(time.Ticks, TimeSpan.Zero);

    public static implicit operator DateTime(SnapshotTimeUtc time) => new DateTime(time.Ticks, DateTimeKind.Utc);

    public static implicit operator SnapshotTimeUtc(DateTimeOffset time) => new SnapshotTimeUtc(time.UtcTicks);

    public static SnapshotTimeUtc Parse(string value) => DateTimeOffset.ParseExact(value, Format, null);

    public string ToQuery(string prefix = "")
    {
        if (!IsValid) return "";

        return $"{prefix}{QueryKey}={ToString()}";
    }

    public override string ToString()
    {
        DateTimeOffset dt = this;
        return dt.ToString(Format);
    }

    public string ToPathString()
    {
        DateTimeOffset dt = this;
        return dt.ToString(PathFormat);
    }

    public static SnapshotTimeUtc ConvertFromJson(DateTimeOffset jsonFormat)
    {
        return new SnapshotTimeUtc(jsonFormat.UtcTicks);
    }

    public DateTimeOffset ConvertToJson()
    {
        return this;
    }

    long ISelectComparable<SnapshotTimeUtc, long>.SelectComparable()
    {
        return Ticks;
    }
}
