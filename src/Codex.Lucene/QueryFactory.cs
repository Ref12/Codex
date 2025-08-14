using Codex.Lucene.Framework.AutoPrefix;
using Codex.Search;
using Codex.Utilities;
using Lucene.Net;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace Codex.Lucene.Search
{
    public class QueryFactory : IQueryFactory<Query>
    {
        public static readonly QueryFactory Instance = new QueryFactory();

        public Query TermQuery(IMappingField mapping, ReadOnlyMemory<byte> term)
        {
            return BinaryItemTermQuery(mapping, BinaryItem.Create(term));
        }

        public Query TermQuery(IMappingField mapping, MurmurHash term)
        {
            return TermQuery(mapping, term.ToShortHash());
        }

        public Query TermQuery(IMappingField mapping, ShortHash term)
        {
            return BinaryItemTermQuery(mapping, BinaryItem.Create(term));
        }

        public Query TermQuery(IMappingField mapping, string term)
        {
            var info = mapping.BehaviorInfo;

            term = term?.ToLowerInvariant() ?? string.Empty;
            term = term.Trim();

            if (!string.IsNullOrEmpty(term))
            {
                if (info.TryGetBinaryValue(term, out var binaryValue))
                {
                    return BinaryItemTermQuery(mapping, BinaryItem.Create(binaryValue.Values, binaryValue.Length));
                }

                switch (mapping.Behavior)
                {
                    case SearchBehavior.PrefixTerm:
                    case SearchBehavior.PrefixShortName:
                    case SearchBehavior.PrefixFullName:
                        term = SearchUtilities.GetNameTransformedValue(term);
                        if (mapping.Behavior == SearchBehavior.PrefixFullName)
                        {
                            term = SearchUtilities.GetHashedQualifiedNameValue(term);
                        }
                        return new AutoPrefixQuery(new Term(mapping.Name, term));
                    case SearchBehavior.Term:
                    case SearchBehavior.NormalizedKeyword:
                    case SearchBehavior.Sortword:
                        if (DocumentVisitor.TryGetShorterHash(term, out var hash))
                        {
                            return BinaryItemTermQuery(mapping, BinaryItem.Create(hash));
                        }
                        break;
                        
                }
            }

            return TermQuery(mapping, new BytesRef(term));
        }

        public Query BinaryItemTermQuery<T>(IMappingField mapping, T binaryItem)
            where T : struct, IBinaryItem<T>
        {
            return TermQuery(mapping, binaryItem.ToBytes());
        }

        public Query TermQuery(IMappingField mapping, BytesRef term)
        {
            return new TermQuery(new Term(mapping.Name, term))
            {
                // Disabling prebuild context to allow parallelism
                // of term lookup in segments
                PrebuildTermContext = false
            };
        }

        public Query TermQuery(IMappingField mapping, bool term)
        {
            return TermQuery(mapping, term ? bool.TrueString : bool.FalseString);
        }

        public Query TermQuery(IMappingField mapping, long term)
        {
            return BinaryItemTermQuery(mapping, DocumentVisitor.GetInt64Term(term));
        }

        public Query TermQuery(IMappingField mapping, SymbolId term)
        {
            return TermQuery(mapping, term.Value);
        }

        public Query TermQuery(IMappingField mapping, DateTime term)
        {
            throw new NotImplementedException();
        }

        public Query TermQuery(IMappingField mapping, int term)
        {
            if (mapping.BehaviorInfo.IsStableId)
            {
                return new FilteredQuery(new MatchAllDocsQuery(), new StableIdTermFilter(mapping.Name, term));
            }

            return TermQuery(mapping, (long)term);
        }

        public Query TermQuery(IMappingField mapping, ReferenceKind term)
        {
            return TermQuery(mapping, term.ToString());
        }

        public Query TermQuery(IMappingField mapping, StringEnum<SymbolKinds> term)
        {
            return TermQuery(mapping, term.ToDisplayString());
        }

        public Query TermQuery(IMappingField mapping, ReferenceKindSet term)
        {
            throw new NotImplementedException();
        }

        public Query TermQuery(IMappingField mapping, StringEnum<PropertyKey> term)
        {
            return TermQuery(mapping, term.ToDisplayString());
        }

        public Query TermQuery(IMappingField mapping, TextSourceBase term)
        {
            return TermQuery(mapping, term.GetString());
        }
    }
}
