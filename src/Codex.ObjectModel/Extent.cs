using Codex.ObjectModel;
using Codex.Utilities.Serialization;

namespace Codex.Utilities
{
    public record struct Extent<T>(Extent Value) : IJsonConvertible<Extent<T>, Extent>
    {
        public int Start => Value.Start;

        public int Length = Value.Length;

        public int EndExclusive => Value.EndExclusive;

        public static implicit operator Extent<T>(Extent r)
        {
            return new Extent<T>(r);
        }

        public static Type JsonFormatType => typeof(Extent);

        public static Extent<T> ConvertFromJson(Extent jsonFormat)
        {
            return new Extent<T>(jsonFormat);
        }

        public ObjectContentLink<T> AsLink(string objectId)
        {
            return new(objectId, Value);
        }

        public Extent ConvertToJson()
        {
            return Value;
        }
    }
}

