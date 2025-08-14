namespace Codex.Lucene.Search
{
    public record struct PendingSegment(Lazy<PageFileSegment> Lazy, ValueTask<PageFileSegment> Task)
    {
        public static implicit operator PendingSegment(Lazy<PageFileSegment> value)
        {
            return new PendingSegment(value, default);
        }

        public static implicit operator PendingSegment(ValueTask<PageFileSegment> value)
        {
            return new PendingSegment(null, value);
        }

        public PageFileSegment GetValue()
        {
            return Lazy != null ? Lazy.Value : Task.GetAwaiter().GetResult();
        }
    }
}
