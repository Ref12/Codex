using Codex.Utilities.Serialization;

namespace Codex.Utilities;

public delegate TResult SpanFunc<TSpan, TResult>(Span<TSpan> span);
public delegate TResult SpanFunc<TSpan, T1, TResult>(Span<TSpan> span, T1 arg1);

public delegate TResult ReadOnlySpanFunc<TSpan, T1, TResult>(ReadOnlySpan<TSpan> span, T1 arg1);
public delegate void ReadOnlySpanAction<TSpan, T1>(ReadOnlySpan<TSpan> span, T1 arg1);

public delegate TResult SpanReaderFunc<TResult>(ref SpanReader reader);
public delegate void SpanWriterAction<T>(ref SpanWriter writer, T arg);

public delegate Span<TResult> SpanResultFunc<TResult>();
public delegate Span<TResult> SpanResultFunc<T, TResult>(T arg);

public delegate ReadOnlySpan<TResult> ReadOnlySpanResultFunc<TResult>();
public delegate ReadOnlySpan<TResult> ReadOnlySpanResultFunc<T, TResult>(in T arg);
public delegate ReadOnlySpan<TResult> ReadOnlySpanResultInFunc<T, TResult>(In<T> arg);

