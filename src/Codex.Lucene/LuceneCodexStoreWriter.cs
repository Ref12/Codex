using System.Diagnostics.ContractsLight;
using System.Text.Json;
using Codex.Logging;
using Codex.Sdk.Utilities;
using Codex.Storage;
using Codex.Utilities;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Util;

namespace Codex.Lucene.Search
{
    public class LuceneCodexStoreWriter : ICodexStoreWriter
    {
        public LuceneWriteConfiguration Configuration { get; }

        protected readonly IRepositoryStoreInfo storeInfo;
        public Logger Logger { get; }

        private LazySearchTypesMap<IndexWriter> Writers => Store.Writers;
        public LuceneCodexStore Store { get; }
        public IStableIdStorage IdTracker => Store.IdTracker;

        public LuceneCodexStoreWriter(LuceneCodexStore store, IRepositoryStoreInfo storeInfo)
        {
            Logger = store.Configuration.Logger;
            this.storeInfo = storeInfo;

            Configuration = store.Configuration;

            Store = store;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task FinalizeAsync()
        {
            Logger.Flush(disableQueuing: true);
        }

        public ValueTask AddAsync<T>(SearchType<T> searchType, T entity, IndexAddOptions options = default)
            where T : class, ISearchEntity
        {
            Add(searchType, entity, options);
            return ValueTask.CompletedTask;
        }

        public void Add<T>(SearchType<T> searchType, T entity, IndexAddOptions options = default)
            where T : class, ISearchEntity
        {
            Document doc = new Document();
            using var context = Pools.EncoderContextPool.Acquire();

            if (!options.StoredExternally)
            {
                var instance = context.Instance;

                entity.SerializeEntityTo(instance.Stream, ObjectStage.Index);

                bool gotBuffer = instance.Stream.TryGetBuffer(out var buffer);
                Contract.Check(gotBuffer)?.Assert("Expected buffer");

                var field = new StoredField(LuceneConstants.SourceFieldName, new BytesRef(buffer.Array, buffer.Offset, buffer.Count));
                doc.Add(field);

                WriteDebugObject(searchType, entity, instance.Stream);
            }

            var visitor = new DocumentVisitor(doc);
            searchType.VisitFields(entity, visitor);

            Writers[searchType].AddDocument(doc);
        }

        private void WriteDebugObject<T>(SearchType<T> searchType, T entity, Stream source)
            where T : class, ISearchEntity
        {
            if (Configuration.DebugStorage == null) return;

            var path = GetObjectPath(searchType, entity);
            if (path == null) return;

            source.Position = 0;
            using var document = JsonDocument.Parse(source, new JsonDocumentOptions()
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            using var context = Pools.EncoderContextPool.Acquire();
            var instance = context.Instance;

            JsonSerializationUtilities.SerializeEntityTo(document, instance.Stream, ObjectStage.Index, JsonFlags.Indented);

            Configuration.DebugStorage.Write(path, instance.Stream);
        }

        public string GetObjectPath<T>(SearchType<T> searchType, T entity, bool forceUnique = false)
            where T : class, ISearchEntity
        {
            return ObjectPaths.GetObjectPath(storeInfo.Repository.Name, searchType, entity, Configuration.EnsureUniquePaths);
        }

        public AsyncLocalScope<EntityBase> EnterRootEntityScope(EntityBase rootEntity)
        {
            return default;
        }
    }
}