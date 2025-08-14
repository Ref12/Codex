using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Codex.Lucene.Framework.AutoPrefix;
using Codex.Utilities;
using CommunityToolkit.HighPerformance;
using Lucene.Net.Documents;
using Lucene.Net.Util;

namespace Lucene.Net;

public static class BinaryField
{
    public static FieldType FIELD_TYPE_NOT_STORED = new FieldType(StringField.TYPE_NOT_STORED) { IsTokenized = true }.AsFrozen();
    public static FieldType FIELD_TYPE_STORED = new FieldType(StringField.TYPE_STORED) { IsTokenized = true }.AsFrozen();

    public static Field CreateBinaryField<TBinaryItem>(this IEnumerable<TBinaryItem> items, string name, bool store = false)
        where TBinaryItem : struct, IBinaryItem<TBinaryItem>
    {
        return new Field(name, items.CreateTokenStream(), store ? FIELD_TYPE_STORED : FIELD_TYPE_NOT_STORED);
    }

    public static Field CreateBinaryField<TBinaryItem>(this TBinaryItem items, string name, bool store = false)
        where TBinaryItem : struct, IBinaryItem<TBinaryItem>
    {
        return items.AsSingle().CreateBinaryField(name, store);
    }

    public static Field CreateBinaryValueField<T>(this T value, string name, bool store = false)
        where T : unmanaged
    {
        return BinaryItem.Create(value).CreateBinaryField(name, store);
    }

    public static Field CreateBinaryField(string name, ReadOnlyMemory<byte> bytes, bool store = false)
    {
        return BinaryItem.Create(bytes).CreateBinaryField(name, store);
    }

    public static BytesRefString ToBytes<T>(this T binaryItem)
        where T : struct, IBinaryItem<T>
    {
        var bytes = new byte[binaryItem.Length];
        binaryItem.CopyTo(bytes);
        return new BytesRefString(new BytesRef(bytes));
    }
}
