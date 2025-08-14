using Codex.Utilities;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace Codex.Lucene.Framework.AutoPrefix
{
    public record AutoPrefixTermsValidator(TermsEnum InputTerms, TermsEnum ActualTerms, int ActualTermCount, int MaxDoc)
    {
        public enum ErrorType { MissingDoc, UnexpectedDoc, MissingTerm, UnexpectedTerm }

        public record struct ErrorArgs(BytesRefString Term, int DocId, ErrorType Type);

        public event Action<ErrorArgs> OnError;

        public bool Run()
        {
            var termStore = new ValidatingTermStore(this, ActualTerms);
            var consumer = new AutoPrefixTermsConsumer(null, termStore, MaxDoc + 1, validating: true);
            termStore.TermsGenerator = consumer;

            foreach (var term in InputTerms.Enumerate())
            {
                var docSet = consumer.StartTerm(term);
                var docs = InputTerms.Docs();
                while (Out.Var(out var docId, docs.NextDoc()) != DocIdSetIterator.NO_MORE_DOCS)
                {
                    docSet.StartDoc(docId, 1);
                    docSet.FinishDoc();
                }

                consumer.FinishTerm(term, default);
            }

            consumer.Finish(0, 0, MaxDoc + 1);

            return termStore.ErrorCount == 0;
        }

        private class ValidatingTermStore(AutoPrefixTermsValidator host, TermsEnum actualTermsEnum) : IOrderingTermStore
        {
            public AutoPrefixTermsConsumer TermsGenerator { get; set; }

            public ArrayBuilder<BytesRef> termStack = new ArrayBuilder<BytesRef>() { new BytesRef() };

            public int FoundTermCount { get; private set; }

            private readonly BytesRef Empty = new BytesRef();

            private BytesRef ActualTerm => actualTermsEnum.Term ?? Empty;
            private BytesRef ResumeActualTerm = new BytesRef();

            public int ErrorCount;

            private readonly OpenBitSet foundTerms = new OpenBitSet(host.ActualTermCount).With(o =>
            {
                if (host.ActualTermCount > 0) o.Set(0, host.ActualTermCount - 1);
            });

            public void ForEachTerm(Action<(BytesRef term, DocIdSet docs)> action)
            {
                while (termStack.Count > 1)
                {
                    var top = termStack.Last;
                    termStack.SetLength(termStack.Count - 1);
                    OnError(new(top, -1, ErrorType.UnexpectedTerm));
                }

                if (FoundTermCount > host.ActualTermCount)
                {
                    OnError(new(default, -1, ErrorType.MissingTerm));
                }
                else if (FoundTermCount < host.ActualTermCount)
                {
                    OnError(new(default, -1, ErrorType.UnexpectedTerm));
                }
            }

            private bool SeekExactActualTerm(BytesRefString soughtActualTerm, out bool pop)
            {
                pop = false;
                if (ActualTerm == soughtActualTerm)
                {
                    return true;
                }
                else if (soughtActualTerm > ActualTerm && soughtActualTerm > ResumeActualTerm)
                {
                    if (ResumeActualTerm.Length > 0)
                    {
                        actualTermsEnum.SeekExact(ResumeActualTerm);
                        ResumeActualTerm.Length = 0;
                    }

                    while (actualTermsEnum.MoveNext() && soughtActualTerm > ActualTerm)
                    {
                        var node = TermsGenerator.CurrentNode;
                        var top = termStack.Last;
                        if (top.IsPrefixOf(ActualTerm) && ActualTerm.IsPrefixOf(soughtActualTerm))
                        {
                            var index = termStack.Count;
                            termStack.SetLength(index + 1);
                            ref var item = ref termStack[index];
                            item ??= new BytesRef();
                            item.CopyBytes(ActualTerm);
                        }
                        else
                        {
                            OnError(new(ActualTerm, -1, ErrorType.UnexpectedTerm));
                        }
                    }

                    return soughtActualTerm == actualTermsEnum.Term;
                }
                else // if (soughtActualTerm < ActualTerm)
                {
                    if (ResumeActualTerm.Length == 0) ResumeActualTerm.CopyBytes(ActualTerm);

                    if (termStack.Count > 1)
                    {
                        var top = termStack.Last;
                        termStack.SetLength(termStack.Count - 1);
                        
                        if (!top.IsPrefixOf(soughtActualTerm))
                        {
                            OnError(new(top, -1, ErrorType.UnexpectedTerm));
                        }
                    }
                    else
                    {
                        OnError(new(soughtActualTerm, -1, ErrorType.UnexpectedTerm));
                    }

                    return actualTermsEnum.SeekExact(soughtActualTerm);
                }
            }

            public void Store(BytesRef term, DocIdSet expectedDocs)
            {
                if (!SeekExactActualTerm(term, out var pop))
                {
                    OnError(new(term, -1, ErrorType.MissingTerm));
                    return;
                }

                FoundTermCount++;

                IEnumerable<int> expectedDocsEnum = expectedDocs.Enumerate();
                IEnumerable<int> actualDocsEnum = actualTermsEnum.Docs().Enumerate();
                foreach (var entry in CollectionUtilities.DistinctMergeSorted(actualDocsEnum.GetIterator(), expectedDocsEnum.GetIterator(), Comparer<int>.Default))
                {
                    // Left is actual, right is expected

                    if (entry.mode == CollectionUtilities.MergeMode.LeftOnly)
                    {
                        OnError(new(term, entry.left, ErrorType.UnexpectedDoc));
                    }
                    else if (entry.mode == CollectionUtilities.MergeMode.RightOnly)
                    {
                        OnError(new(term, entry.left, ErrorType.MissingDoc));
                    }
                }
            }

            public void OnError(ErrorArgs args)
            {
                ErrorCount++;
                host.OnError?.Invoke(args);
            }
        }
    }
}
