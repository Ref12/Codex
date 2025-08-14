namespace Codex.Utilities;

public record SegmentedListWriter<T>(int SegmentCount = 1, bool TrackSegments = false, ArrayBuilder<T> Items = null)
{
    private int _lastSegmentEnd = 0;

    public ArrayBuilder<ListSegment<T>> TrackedSegments { get; } = TrackSegments ? new(SegmentCount) : null;

    public ArrayBuilder<T> Items { get; } = Items ?? new ArrayBuilder<T>(SegmentCount);

    public SegmentedListWriter(ArrayBuilder<T> items) 
        : this(0, Items: items)
    {

    }

    public void Add(T item)
    {
        Items.Add(item);
    }

    public ListSegment<T> CurrentSegment => new ListSegment<T>(Items, Extent.FromBounds(_lastSegmentEnd, Items.Count));

    public void StartSegment()
    {
        _lastSegmentEnd = Items.Count;
    }

    public ListSegment<T> CreateSegment(bool advance = true)
    {
        if (!TrackSegments && _lastSegmentEnd == Items.Count)
        {
            return default;
        }

        var segment = CurrentSegment;

        if (TrackSegments)
        {
            TrackedSegments.Add(segment);
        }

        if (advance)
        {
            StartSegment();
        }
        return segment;
    }
}