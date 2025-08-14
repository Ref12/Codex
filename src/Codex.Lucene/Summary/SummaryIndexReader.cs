using Codex.Utilities;
using System.Diagnostics.ContractsLight;
using System.Runtime.InteropServices;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Util;
using CommunityToolkit.HighPerformance;
using Codex.Lucene.Search;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Search;
using Codex.Lucene.Framework;
using Codex.Sdk.Search;
using Lucene.Net.QueryParsers.Simple;
using Codex.Lucene.Framework.AutoPrefix;
using System.Collections;
using LibGit2Sharp;
using System.Collections.Specialized;
using Codex.Sdk.Utilities;
using System.Text.Unicode;
using System.Text;
using Lucene.Net;
using System.Runtime.CompilerServices;

namespace Codex.Lucene;

using static Codex.Lucene.Search.LuceneConstants;


public class SummaryIndexReader
{
    Dictionary<SegmentName, (DocId DocId, int Ordinal)> SegmentMap { get; } = new();

    int[] SegmentDocIdToOrdinalMap;

    public DirectoryReader SummaryReader { get; }
    public DirectoryReader MainReader { get; }
    public IndexSearcher MainSearcher { get; }
    public IndexSearcher SummarySearcher { get; }

    public SearchType SearchType { get; }

    public SummaryIndexReader(LuceneConfiguration configuration, SearchType searchType)
    {
        SearchType = searchType;
        MainReader = configuration.OpenReader(searchType, summary: false);
        MainSearcher = new IndexSearcher(MainReader, TaskScheduler.Default);

        SummaryReader = configuration.OpenReader(searchType, summary: true);
        SummarySearcher = new IndexSearcher(SummaryReader);

        var segmentMap = LoadSegmentMap(summaryReader: SummaryReader);

        SegmentDocIdToOrdinalMap = new int[SummaryReader.MaxDoc];
        Array.Fill(SegmentDocIdToOrdinalMap, -1);

        foreach (var context in MainReader.Leaves)
        {
            var docId = segmentMap[context.AtomicReader.Name];
            SegmentMap[context.AtomicReader.Name] = (docId, context.Ord);
            SegmentDocIdToOrdinalMap[docId] = context.Ord;
        }
    }

    public Query FromCodexQuery<T>(CodexQuery<T> query)
    {
        var termCollector = new TermCollector(this);
        QueryConverter.FromCodexQuery<T>(query, new QueryState<T>(Rewrite: termCollector.Rewrite));

        termCollector.ComputeExclusions();

        termCollector.IsRewriting = true;
        return QueryConverter.FromCodexQuery<T>(query, new QueryState<T>(Rewrite: termCollector.Rewrite));
    }

    private record TermCollector(SummaryIndexReader Reader)
    {
        public Dictionary<Term, SegmentBitMap> AllTerms { get; } = new();
        public Dictionary<FieldTermNGram, SegmentBitMap> AllNGrams { get; } = new();
        public List<FieldTermNGram> SortedFieldGrams { get; } = new();
        public HashSet<Term> TermBuffer { get; } = new();
        public Dictionary<string, SegmentBitMap> FieldMatches { get; } = new();
        public bool IsRewriting { get; set; }

        public int SegmentCount;

        public static readonly IComparer<FieldTermNGram> FieldGramComparer = new ComparerBuilder<FieldTermNGram>()
            .CompareByAfter(f => f.Field)
            .CompareByAfter(f => f.NGram);

        public void ComputeExclusions()
        {
            SortedFieldGrams.Capacity = AllNGrams.Count;
            SortedFieldGrams.AddRange(AllNGrams.Keys);
            SortedFieldGrams.Sort(FieldGramComparer);

            var mainReaderSegments = Reader.MainReader.Leaves;
            var summarySegments = Reader.SummaryReader.Leaves;
            SegmentCount = mainReaderSegments.Count;
            TermsEnum termsEnum = null;
            DocsEnum docEnum = null;
            BytesRefString bytesRef = new BytesRef(SummaryNGramSize);
            var ngramMatches = new SegmentBitMap?[SortedFieldGrams.Count];

            void setByDocId(DocId docId, ref SegmentBitMap bitMap, bool value)
            {
                var ord = Reader.SegmentDocIdToOrdinalMap[docId];
                if (ord >= 0)
                {
                    bitMap.Set(ord, value);
                }
            }

            foreach ((var field, var fieldGrams) in SortedFieldGrams.SortedGroupBy(f => f.Field))
            {
                foreach (var segment in mainReaderSegments)
                {
                    var terms = segment.AtomicReader.GetTerms(field);
                    var termCount = terms?.Count;
                }

                foreach (var segment in summarySegments)
                {
                    var ord = segment.Ord;
                    var terms = segment.AtomicReader.GetTerms(field);
                    if (terms == null)
                    {
                        // No terms for this field in the segment.
                        continue;
                    }

                    var termCount = terms.Count;
                    termsEnum = terms.GetIterator(termsEnum);
                    foreach ((var fieldGram, var index) in fieldGrams.WithAbsoluteIndices())
                    {
                        fieldGram.NGram.SetInto(bytesRef);
                        ref var match = ref ngramMatches[index];
                        var localMatch = match ??= SegmentBitMap.Create(SegmentCount);

                        if (termsEnum.SeekExact(bytesRef))
                        {
                            docEnum = termsEnum.Docs(liveDocs: null, docEnum, DocsFlags.NONE);
                            while (Out.Var(out var docId, docEnum.NextDoc()) != DocIdSetIterator.NO_MORE_DOCS)
                            {
                                setByDocId(docId, ref localMatch, true);
                            }
                        }

                        match = localMatch;
                    }
                }
            }

            var empty = SegmentBitMap.Create(SegmentCount);
            for (int i = 0; i < SortedFieldGrams.Count; i++)
            {
                var fieldGram = SortedFieldGrams[i];
                var match = ngramMatches[i] ?? empty;
                AllNGrams[fieldGram] = match;
            }

            AggregateMatches();
        }

        private void AggregateMatches()
        {
            var empty = SegmentBitMap.Create(SegmentCount);
            var matchBox = Box.Create<SegmentBitMap?>();
            foreach (var term in AllTerms.Keys.ToList())
            {
                matchBox.Value = null;
                GetNGrams(term.Bytes, (term, matchBox, AllNGrams), add: static (ngram, data) =>
                {
                    var ngramMatches = data.AllNGrams[new FieldTermNGram(data.term.Field, ngram)];
                    if (data.matchBox.Value == null)
                    {
                        data.matchBox.Value = ngramMatches;
                    }
                    else
                    {
                        data.matchBox.Value = data.matchBox.Value.Value.Or(ngramMatches);
                    }
                });

                AllTerms[term] = matchBox.Value ?? empty;
            }
        }

        public Query Rewrite<T>(Query query, CodexQuery<T> codexQuery, QueryState<T> state)
        {
            if (!IsRewritable(codexQuery.Kind)) return query;

            TermBuffer.Clear();
            query.ExtractTerms(TermBuffer);

            if (TermBuffer.Count == 0)
            {
                // No terms don't rewrite query
                return query;
            }

            if (!IsRewriting)
            {
                // Collect terms and ngrams
                foreach (var term in TermBuffer)
                {
                    if (!AllTerms.TryGetValue(term, out var termInfo))
                    {
                        AllTerms.Add(term, default);

                        // TODO: In theory this can be done during exclusion computation
                        GetNGrams(term.Bytes, (term, AllNGrams), static (ngram, data) => data.AllNGrams.TryAdd(new(data.term.Field, ngram), default));
                    }
                }
            }
            else
            {
                SegmentBitMap? segmentMatches = null;
                foreach (var term in TermBuffer)
                {
                    segmentMatches &= AllTerms[term];
                }

                var exclusion = segmentMatches.Value.Overview;
                if (exclusion.IsNone)
                {
                    // All segments excluded
                    return new BooleanQuery();
                }
                else if (exclusion.IsSomeNotAll)
                {
                    return new SummaryQuery(query, new SummaryQueryState(segmentMatches.Value, InnerCanRewrite: MayRewrite(codexQuery.Kind, query)));
                }
            }

            return query;
        }

        private bool MayRewrite(CodexQueryKind kind, Query query)
        {
            return kind != CodexQueryKind.Term || query is MultiTermQuery;
        }

        public bool IsRewritable(CodexQueryKind kind)
        {
            switch (kind)
            {
                case CodexQueryKind.And:
                case CodexQueryKind.Or:
                case CodexQueryKind.Negate:
                case CodexQueryKind.Boosting:
                    return false;
                case CodexQueryKind.Term:
                case CodexQueryKind.MatchPhrase:
                    return true;
                default:
                    throw Contract.AssertFailure($"Unhandled CodexQueryKind: {kind}");
            }
        }
    }

    public static void Update(LuceneConfiguration configuration, SearchType searchType)
    {
        var summaryDir = configuration.OpenIndexDirectory(searchType, summary: true);
        using var summaryWriter = new IndexWriter(summaryDir, new IndexWriterConfig(CurrentVersion, new KeywordAnalyzer()));

        using var mainReader = configuration.OpenReader(searchType);

        Dictionary<SegmentName, SegmentReader> currentMainSegments = mainReader.Leaves.Select(r => (SegmentReader)r.AtomicReader)
            .ToDictionary(r => new SegmentName(r.SegmentName));

        Dictionary<SegmentName, DocId> segmentMap;
        using (var summaryReader = summaryWriter.GetReaderNoFlush())
        {
            segmentMap = LoadSegmentMap(summaryReader);

            foreach ((var mainSegmentName, var docId) in segmentMap)
            {
                if (!currentMainSegments.ContainsKey(mainSegmentName))
                {
                    bool succeeded = summaryWriter.TryDeleteDocument(summaryReader, docId);
                    if (!succeeded)
                    {

                    }
                }
            }
        }

        foreach ((var mainSegmentName, var mainSegmentReader) in currentMainSegments)
        {
            if (!segmentMap.ContainsKey(mainSegmentName))
            {
                summaryWriter.AddDocument(CreateSegmentDocument(mainSegmentReader, searchType));
            }
        }
    }

    public static Dictionary<SegmentName, DocId> LoadSegmentMap(DirectoryReader summaryReader)
    {
        Contract.Assert(summaryReader.Context.IsTopLevel);

        var map = new Dictionary<SegmentName, DocId>();
        var liveDocs = MultiFields.GetLiveDocs(summaryReader);
        for (int docId = summaryReader.Context.DocBaseInParent; docId <= summaryReader.MaxDoc - 1; docId++)
        {
            if (liveDocs?.Get(docId) == false) continue;

            var segmentDocument = summaryReader.Document(docId);
            map.Add(segmentDocument.GetField(SummaryIndexSegmentIdFieldName).GetStringValue(), docId);
        }

        return map;
    }

    private static Document CreateSegmentDocument(SegmentReader mainSegmentReader, SearchType searchType)
    {
        var document = new Document();

        document.Add(new StringField(SummaryIndexSegmentIdFieldName, mainSegmentReader.SegmentName, Field.Store.YES));

        TermsEnum reuse = default;
        foreach (var field in searchType.Fields.Values)
        {
            var terms = mainSegmentReader.GetTerms(field.Name);
            if (terms == null) continue;

            AddTerms(document, field, terms, ref reuse);
        }

        return document;
    }

    private static void AddTerms(Document document, SearchField field, Terms terms, ref TermsEnum reuse)
    {
        // Only use full term for low cardinality fields
        Placeholder.Todo("Set info on field about whether it should all summarizing full term");
        //bool useFullTerm = GetUseFullTermOverride() ?? !field.BehaviorInfo.DisallowSummarizeFullTerm && terms.Count < 256;

        HashSet<TermNGram> addedGrams = new HashSet<TermNGram>();
        TermNGram ngramBuffer = new TermNGram();

        Span<byte> byteBuffer = MemoryMarshal.CreateSpan(ref ngramBuffer.Data, 1).AsBytes();
        byteBuffer = byteBuffer.Slice(0, SummaryNGramSize);

        foreach (BytesRefString term in terms.Enumerate(ref reuse))
        {
            GetNGrams(term, ref ngramBuffer, byteBuffer, addedGrams, static (ngram, addedGrams) => addedGrams.Add(ngram));
        }

        document.Add(addedGrams.CreateBinaryField(field.Name, store: false));
    }

    private static void GetNGrams<TArg>(BytesRefString term, TArg arg, Action<TermNGram, TArg> add)
    {
        TermNGram ngramBuffer = new TermNGram();

        Span<byte> byteBuffer = MemoryMarshal.CreateSpan(ref ngramBuffer.Data, 1).AsBytes();
        byteBuffer = byteBuffer.Slice(0, SummaryNGramSize);

        GetNGrams(term, ref ngramBuffer, byteBuffer, arg, add);
    }

    private static void GetNGrams<TArg>(BytesRefString term, ref TermNGram ngramBuffer, Span<byte> byteBuffer, TArg arg, Action<TermNGram, TArg> add)
    {
        var bytes = term.Value;
        ngramBuffer.Data = 0;
        var span = bytes.Span;
        if (span.Length < SummaryNGramSize)
        {
            bytes.Span.CopyTo(byteBuffer);
            ngramBuffer.Length = (byte)span.Length;
            add(ngramBuffer, arg);
            return;
        }

        ngramBuffer.Length = SummaryNGramSize;
        for (int i = SummaryNGramSize; i <= span.Length; i++)
        {
            ngramBuffer.Data = 0;
            bytes.Span.Slice(i - SummaryNGramSize, SummaryNGramSize).CopyTo(byteBuffer);
            add(ngramBuffer, arg);
        }
    }

    private record struct FieldTermNGram(string Field, TermNGram NGram)
    {

    }

    private record struct TermNGram : IBinaryItem<TermNGram>, IComparable<TermNGram>
    {
        public TermNGram(byte data, byte length)
        {
            Data = data;
            Length = length;
        }

        public int Data;
        public byte Length;

        int IBinaryItem.Length => Length;

        public int CompareTo(TermNGram other)
        {
            return Data.ChainCompareTo(other.Data)
                ?? Length.CompareTo(other.Length);
        }

        public void SetInto(BytesRefString bytesRef)
        {
            bytesRef.Value.Length = Length;
            this.CopyTo(bytesRef.Span);
        }

        public override string ToString()
        {
            return Encoding.UTF8.GetString(this.GetSpan());
        }

        public static ReadOnlySpan<byte> GetSpan(in TermNGram self)
        {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(self.Data), 1).AsBytes().Slice(0, self.Length);
        }
    }
}

public interface IAlias<TSelf, T>
    where TSelf : IAlias<TSelf, T>
{
    T Value { get; }

    public static abstract implicit operator TSelf(T value);

    public static abstract implicit operator T(TSelf value);
}

public record struct DocId(int Value) : IAlias<DocId, int>
{
    public static implicit operator DocId(int value)
    {
        return new DocId(value);
    }

    public static implicit operator int(DocId value)
    {
        return value.Value;
    }
}

public record struct SegmentName(string Value) : IEquatable<SegmentName>, IAlias<SegmentName, string>
{
    public bool Equals(SegmentName other)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    }

    public static implicit operator SegmentName(string name)
    {
        return new SegmentName(name);
    }

    public static implicit operator string(SegmentName name)
    {
        return name.Value;
    }
}
