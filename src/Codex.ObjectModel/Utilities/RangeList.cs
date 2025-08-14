using System.Collections;

public sealed record RangeList(Extent Extent) : IReadOnlyList<int>
{
    public int this[int index] => Extent.Start + index;

    public int Count => Extent.Length;

    public IEnumerator<int> GetEnumerator()
    {
        var end = Extent.EndExclusive;
        for (int i = Extent.Start; i < end; i++)
        {
            yield return i;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
