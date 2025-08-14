using System.Numerics;

namespace Codex.ObjectModel
{
    public interface IExternalEntity<TSelf, TLink> : IExternalSearchEntity<TLink>
        where TSelf : IExternalEntity<TSelf, TLink>
    {
        static TLink GetExternalLink(TSelf self) => self.ExternalLink;
    }

    public interface IExternalEntity<TLink>
    {
        // Don't include in hash since the actual content of the entity should determine its identity.
        // Don't include for ObjectStage.Index either since this data should not be serialized as part data inside index.
        // Instead we use a special call to Serialize with ObjectStage.Analysis for this specific interface
        [Include(ObjectStage.Analysis)]
        [UseInterface]
        TLink ExternalLink { get; set; }
    }

    public interface IExternalSearchEntity<TLink> : IExternalEntity<TLink>, ISearchEntity
    {
    }

    //public class ExternalEntity<TLink> : IExternalEntity<ExternalEntity<TLink>, TLink>
    //{
    //    public TLink ExternalLink { get; set; }
    //}

    public record struct Include<T>(uint Flags, SearchType<T> SearchType)// : IBitwiseOperators<RequiredIn<T>, RequiredIn<T>, RequiredIn<T>>
        where T : class, ISearchEntity
    {
        public static Include<T> operator |(Include<T> left, Include<T> right)
        {
            return new Include<T>(left.Flags | right.Flags, left.SearchType);
        }

        public bool HasFlag(Include<T> flag)
        {
            return (Flags & flag.Flags) == flag.Flags;
        }
    }
}
