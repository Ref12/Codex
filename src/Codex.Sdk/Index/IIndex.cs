using Codex.ObjectModel;
using Codex.ObjectModel.Implementation;
using Codex.Storage.BlockLevel;
using Codex.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Search
{
    public partial interface IIndex
    {
    }

    public interface IStoredFilterInfo
    {
        public bool DedupeEntities => true;

        bool IsPossibleProject(string projectId) => true;
    }

    public partial interface IIndex<T> : IIndex
        where T : class, ISearchEntity<T>
    {
        int Count { get; }

        Task<IReadOnlyList<TResult>> GetAsync<TResult>(
            IReadOnlyList<int> docIds,
            QueryOptions<T> options = default)
            where TResult : T;

        Task<IReadOnlyList<T>> GetAsync(IReadOnlyList<int> ids)
        {
            return GetAsync<T>(ids);
        }

        Task<IIndexSearchResponse<TResult>> QueryAsync<TResult>(
            IStoredFilterInfo storedFilterInfo,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> filter,
            OneOrMany<IMappingField<T>> sort = null,
            int? take = null,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> boost = null,
            Func<QueryOptions<T>, QueryOptions<T>> updateOptions = null)
        where TResult : T
        {
            return QueryAsync<TResult>(storedFilterInfo, filter, updateOptions: o => (updateOptions?.Invoke(o) ?? o) with { Sort = sort, Take = take, Boost = boost });
        }

        Task<IIndexSearchResponse<TResult>> QueryAsync<TResult>(
            IStoredFilterInfo storedFilterInfo,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> filter,
            Func<QueryOptions<T>, QueryOptions<T>> updateOptions = null)
        where TResult : T;
    }

    public static partial class IIndexExtensions
    {
        public static Task<IIndexSearchResponse<T>> SearchAsync<T>(
            this IIndex<T> index,
            IStoredFilterInfo storedFilterInfo,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> filter,
            OneOrMany<IMappingField<T>> sort = null,
            int? take = null,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> boost = null,
            Func<QueryOptions<T>, QueryOptions<T>> updateOptions = null)
            where T : class, ISearchEntity<T>
        {
            return index.QueryAsync<T>(
                storedFilterInfo,
                filter,
                sort,
                take,
                boost,
                updateOptions);
        }

        public static Task<IIndexSearchResponse<T>> SearchAsync<T>(
            this IIndex<T> index,
            IStoredFilterInfo storedFilterInfo,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> filter,
            Func<QueryOptions<T>, QueryOptions<T>> updateOptions = null)
            where T : class, ISearchEntity<T>
        {
            return index.QueryAsync<T>(
                storedFilterInfo,
                filter,
                updateOptions);
        }

        //public static Task<IIndexSearchResponse<TResult>> QueryAsync<T, TResult>(
        //    this IIndex<T> index,
        //        IStoredFilterInfo storedFilterInfo,
        //        Func<CodexQueryBuilder<T>, CodexQuery<T>> filter,
        //        OneOrMany<IMappingField<T>> sort = null,
        //        int? take = null,
        //        Func<CodexQueryBuilder<T>, CodexQuery<T>> boost = null)
        //    where TResult : T;

        //public static Task<IIndexSearchResponse<T>> SearchAsync<T>(
        //    IStoredFilterInfo storedFilterInfo,
        //    Func<CodexQueryBuilder<T>, CodexQuery<T>> filter,
        //    OneOrMany<IMappingField<T>> sort = null,
        //    int? take = null,
        //    Func<CodexQueryBuilder<T>, CodexQuery<T>> boost = null)
        //{
        //    return index.QueryAsync<T>(
        //        storedFilterInfo,
        //        filter,
        //        sort,
        //        take,
        //        boost);
        //}
    }

    public record struct QueryOptions<T>(
        Include<T>? Includes = null, 
        OneOrMany<IMappingField<T>> Sort = null,
        int? Take = null,
        Func<CodexQueryBuilder<T>, CodexQuery<T>> Boost = null,
        ISortField<T, string> ProjectSortField = null,
        IMappingField<T> HighlightField = null,
        AddressKind AddressKind = AddressKind.Default)
        where T : class, ISearchEntity;

    public class OneOrMany<T>
    {
        public T[] Values { get; }

        public OneOrMany(params T[] values)
        {
            Values = values;
        }

        public static implicit operator OneOrMany<T>(T[] values)
        {
            return new OneOrMany<T>(values);
        }

        public static implicit operator OneOrMany<T>(T value)
        {
            return new OneOrMany<T>(value);
        }
    }

    public class AdditionalSearchArguments<T>
    {
        public Func<CodexQueryBuilder<T>, CodexQuery<T>> Boost { get; set; }
        public List<IMappingField<T>> SortFields { get; } = new List<IMappingField<T>>();
        public int? Take { get; set; }
    }

    public interface IIndexSearchResponse<out T>
    {
        IReadOnlyList<ISearchHit<T>> Hits { get; }
        public int Total { get; }
    }

    public class IndexSearchResponse<T> : IIndexSearchResponse<T>
    {
        public List<ISearchHit<T>> Hits { get; set; } = new List<ISearchHit<T>>();

        public int Total { get; set; }

        IReadOnlyList<ISearchHit<T>> IIndexSearchResponse<T>.Hits => Hits;
    }

    public class SearchHit<T> : ISearchHit<T>
    {
        public T Source { get; set; }

        public IEnumerable<TextLineSpan> Highlights { get; set; } = Array.Empty<TextLineSpan>();

        public bool MatchesSecondaryFilter { get; set; }
    }

    public interface ISearchHit<out T>
    {
        T Source { get; }
        IEnumerable<TextLineSpan> Highlights { get; }
        bool MatchesSecondaryFilter { get; set; }
    }

    public enum CodexQueryKind
    {
        None,
        And,
        Or,
        Term,
        Negate,
        MatchPhrase,
        Boosting,
        All
    }

    public class CodexQueryBuilder<T>
    {
        public CodexQuery<T> None()
        {
            return StaticMatchQuery<T>.None;
        }

        public CodexQuery<T> All()
        {
            return StaticMatchQuery<T>.All;
        }

        public virtual CodexQuery<T> Term<TValue>(IMappingField<T, TValue> mapping, TValue term, float? boost = null, bool include = true)
        {
            if (term == null)
            {
                return null;
            }
            else if (!include || ((term is string s) && s == ""))
            {
                return None();
            }

            return new TermCodexQuery<T, TValue>(mapping, term)
            {
                BoostValue = boost
            };
        }

        public virtual CodexQuery<T> MatchPhrase(IMappingField mapping, string phrase)
        {
            return MatchPhrasePrefix(mapping, phrase, maxExpansions: 0);
        }

        public virtual CodexQuery<T> MatchPhrasePrefix(IMappingField mapping, string phrase, int maxExpansions)
        {
            return new MatchPhraseCodexQuery<T>(mapping, phrase, maxExpansions);
        }

        public virtual CodexQuery<T> Terms<TValue>(IMappingField<T, TValue> mapping, params TValue[] terms)
        {
            return Terms(mapping, terms.AsEnumerable());
        }

        public virtual CodexQuery<T> Terms<TValue>(IMappingField<T, TValue> mapping, IEnumerable<TValue> terms)
        {
            if (terms == null)
            {
                return null;
            }

            CodexQuery<T> q = null;
            foreach (var term in terms)
            {
                q |= Term(mapping, term);
            }

            return q;
        }
    }

    public abstract class CodexQuery<T> : IEquatable<CodexQuery<T>>
    {
        public IMappingField? Field { get; protected init; }

        public CodexQueryKind Kind { get; }

        public float? BoostValue { get; set; }

        public CodexQuery(CodexQueryKind kind)
        {
            Kind = kind;
        }

        public CodexQuery<T> Boost(float boost)
        {
            BoostValue = boost;
            return this;
        }

        public static CodexQuery<T> operator &(CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
        {
            if (leftQuery?.Kind == CodexQueryKind.None || rightQuery?.Kind == CodexQueryKind.None)
            {
                return StaticMatchQuery<T>.None;
            }

            if (leftQuery == null || leftQuery.Kind == CodexQueryKind.All) return rightQuery;
            else if (rightQuery == null || rightQuery.Kind == CodexQueryKind.All) return leftQuery;

            return new BinaryCodexQuery<T>(CodexQueryKind.And, leftQuery, rightQuery);
        }

        public static CodexQuery<T> operator |(CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
        {
            if (leftQuery?.Kind == CodexQueryKind.All || rightQuery?.Kind == CodexQueryKind.All)
            {
                return StaticMatchQuery<T>.All;
            }
            else if (leftQuery == null || leftQuery.Kind == CodexQueryKind.None) return rightQuery;
            else if (rightQuery == null || rightQuery.Kind == CodexQueryKind.None) return leftQuery;

            return new BinaryCodexQuery<T>(CodexQueryKind.Or, leftQuery, rightQuery);
        }

        public static CodexQuery<T> operator +(CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
        {
            if (leftQuery == null) return null;
            else if (rightQuery == null || rightQuery.Kind == CodexQueryKind.None) return leftQuery;

            return new BoostingCodexQuery<T>(leftQuery, rightQuery)
            {
                BoostValue = rightQuery.BoostValue
            };
        }

        public static CodexQuery<T> operator !(CodexQuery<T> query)
        {
            if (query == null) return null;

            return new NegateCodexQuery<T>(query);
        }

        public abstract bool Equals(CodexQuery<T> other);
    }

    public interface ITermQuery
    {
        IMappingField Field { get; }

        TQuery CreateQuery<TQuery>(IQueryFactory<TQuery> factory);
    }

    public abstract class CodexQuery<TQuery, T> : CodexQuery<T>
        where TQuery : CodexQuery<TQuery, T>
    {
        private static EqualityComparerBuilder<TQuery> s_comparer;

        protected CodexQuery(CodexQueryKind kind) 
            : base(kind)
        {
        }

        protected abstract void DefineEquality(EqualityComparerBuilder<TQuery> queryComparer);

        public override bool Equals(CodexQuery<T> other)
        {
            if (other is TQuery otherQuery)
            {
                InitComparer();

                return s_comparer.Equals((TQuery)this, otherQuery);
            }

            return false;
        }

        public override int GetHashCode()
        {
            InitComparer();
            return s_comparer.GetHashCode((TQuery)this);
        }

        private void InitComparer()
        {
            if (s_comparer == null)
            {
                var comparer = new EqualityComparerBuilder<TQuery>()
                    .CompareByAfter(q => q.Kind);

                DefineEquality(comparer);
                s_comparer = comparer;
            }
        }
    }

    public class StaticMatchQuery<T> : CodexQuery<StaticMatchQuery<T>, T>
    {
        public static StaticMatchQuery<T> None { get; } = new StaticMatchQuery<T>(CodexQueryKind.None);
        public static StaticMatchQuery<T> All { get; } = new StaticMatchQuery<T>(CodexQueryKind.All);

        public StaticMatchQuery(CodexQueryKind kind)
            : base(kind)
        {
        }

        protected override void DefineEquality(EqualityComparerBuilder<StaticMatchQuery<T>> queryComparer)
        {
        }
    }

    public class TermCodexQuery<T, TValue> : CodexQuery<TermCodexQuery<T, TValue>, T>, ITermQuery
    {
        public TValue Term { get; }

        public TermCodexQuery(IMappingField field, TValue term)
            : base(CodexQueryKind.Term)
        {
            Field = field;
            Term = term;
        }

        public TQuery CreateQuery<TQuery>(IQueryFactory<TQuery> factory)
        {
            return ((IQueryFactory<TQuery, TValue>)factory).TermQuery(Field, Term);
        }

        protected override void DefineEquality(EqualityComparerBuilder<TermCodexQuery<T, TValue>> queryComparer)
        {
            queryComparer
                .CompareByAfter(s => s.Field, ReferenceEqualityComparer.Instance)
                .CompareByAfter(s => s.Term);
        }
    }

    public class NegateCodexQuery<T> : CodexQuery<NegateCodexQuery<T>, T>
    {
        public CodexQuery<T> InnerQuery { get; }

        public NegateCodexQuery(CodexQuery<T> innerQuery)
            : base(CodexQueryKind.Negate)
        {
            InnerQuery = innerQuery;
        }

        protected override void DefineEquality(EqualityComparerBuilder<NegateCodexQuery<T>> queryComparer)
        {
            queryComparer.CompareByAfter(s => s.InnerQuery);
        }
    }

    public class BoostingCodexQuery<T> : CodexQuery<BoostingCodexQuery<T>, T>
    {
        public CodexQuery<T> InnerQuery { get; }
        public CodexQuery<T> BoostQuery { get; }

        public BoostingCodexQuery(CodexQuery<T> innerQuery, CodexQuery<T> boostQuery)
            : base(CodexQueryKind.Boosting)
        {
            InnerQuery = innerQuery;
            BoostQuery = boostQuery;
        }

        protected override void DefineEquality(EqualityComparerBuilder<BoostingCodexQuery<T>> queryComparer)
        {
            queryComparer
                .CompareByAfter(s => s.InnerQuery)
                .CompareByAfter(s => s.BoostQuery);
        }
    }

    public class BinaryCodexQuery<T> : CodexQuery<BinaryCodexQuery<T>, T>
    {
        public CodexQuery<T> LeftQuery { get; }
        public CodexQuery<T> RightQuery { get; }

        public BinaryCodexQuery(CodexQueryKind kind, CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
            : base(kind)
        {
            this.LeftQuery = leftQuery;
            this.RightQuery = rightQuery;
        }

        protected override void DefineEquality(EqualityComparerBuilder<BinaryCodexQuery<T>> queryComparer)
        {
            queryComparer
                .CompareByAfter(q => q.LeftQuery)
                .CompareByAfter(q => q.RightQuery);
        }
    }

    public class MatchPhraseCodexQuery<T> : CodexQuery<MatchPhraseCodexQuery<T>, T>
    {
        public string Phrase { get; }
        public int MaxExpansions { get; }

        public MatchPhraseCodexQuery(IMappingField field, string phrase, int maxExpansions)
            : base(CodexQueryKind.MatchPhrase)
        {
            Field = field;
            Phrase = phrase;
            MaxExpansions = maxExpansions;
        }

        protected override void DefineEquality(EqualityComparerBuilder<MatchPhraseCodexQuery<T>> queryComparer)
        {
            queryComparer
                .CompareByAfter(q => q.Field, ReferenceEqualityComparer.Instance)
                .CompareByAfter(q => q.Phrase)
                .CompareByAfter(q => q.MaxExpansions);
        }


        //public TQuery CreateQuery<TQuery>(IQueryFactory<TQuery> factory)
        //{
        //    return ((IQueryFactory<TQuery, TValue>)factory).TermQuery(Mapping, Term);
        //}
    }
}
