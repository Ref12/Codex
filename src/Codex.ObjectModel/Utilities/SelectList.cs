using System.Collections;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// List which applies selector function on elements of underlying list when accessed
/// </summary>
public sealed class SelectList<T, TResult, TState> : IReadOnlyList<TResult>
{
    /// <summary>
    /// The underlying list
    /// </summary>
    public IReadOnlyList<T> UnderlyingList { get; set; }
    private readonly Func<T, int, TState, TResult> m_selector;

    [MaybeNull]
    private readonly TState m_state;

    /// <nodoc />
    public SelectList(IReadOnlyList<T> underlyingList, Func<T, int, TState, TResult> selector, TState state)
    {
        UnderlyingList = underlyingList;
        m_selector = selector;
        m_state = state;
    }

    /// <inheritdoc />
    public TResult this[int index] => m_selector(UnderlyingList[index], index, m_state!);

    /// <inheritdoc />
    public int Count => UnderlyingList.Count;

    /// <inheritdoc />
    public IEnumerator<TResult> GetEnumerator()
    {
        for (int i = 0; i < UnderlyingList.Count; i++)
        {
            yield return this[i];
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}