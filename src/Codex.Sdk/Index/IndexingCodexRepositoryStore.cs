using System.Collections.Concurrent;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.ObjectModel.Attributes;
using Codex.ObjectModel.Implementation;
using Codex.Sdk.Utilities;
using Codex.Utilities;
using Codex.Utilities.Serialization;

namespace Codex.Storage
{
    public record IndexingCodexRepositoryStore(
        ICodexStoreWriter StoreWriter, 
        Logger Logger, 
        RepositoryStoreInfo StoreInfo) 
        : IndexingCodexRepositoryStoreBase(StoreWriter, Logger, StoreInfo)
    {
        protected override async Task AddProjectAsync(AnalyzedProjectInfo project)
        {
            await AddAsync(SearchTypes.Project, new ProjectSearchModel() { Project = project });

            var kind = project.ProjectKind.Value switch
            {
                ProjectKind.Repo => SymbolKinds.Repo,
                _ => project.ProjectId.Contains('/') ? SymbolKinds.Repo : SymbolKinds.Project
            };

            // Doesn't particularly make sense to create definition for distributed projects
            // since they span repos.
            if (project.ProjectKind.Value?.IsDistributedProject() != true)
            {
                // Synthesize definition for project
                await AddDefinitionsAsync(new[]
                {
                    new DefinitionSymbol()
                    {
                        Id = SymbolId.CreateFromId($"{kind}:" + project.ProjectId),
                        ShortName = project.ProjectId,
                        DisplayName = project.ProjectId,
                        ProjectId = project.RepositoryName,
                        Kind = kind,
                    }
                },
                declared: true);
            }

            await AddDefinitionsAsync(project.Definitions, declared: false);

            foreach (var projectReference in project.ProjectReferences)
            {
                await AddAsync(SearchTypes.ProjectReference, new ProjectReferenceSearchModel(project)
                {
                    ProjectReference = projectReference
                });

                if (projectReference.Definitions.Count != 0)
                {
                    await AddDefinitionsAsync(projectReference.Definitions, declared: false);
                }
            }
        }

        private async Task AddTextFileAsync(
            SourceFile file,
            bool addChunks = true)
        {
            TextIndexingUtilities.ToChunks(file, out var chunkFile, out var chunks, PopulateTextChunk);

            if (!file.ExcludeFromSearch && addChunks)
            {
                foreach ((var chunk, var index) in chunks.WithIndices())
                {
                    await AddAsync(SearchTypes.TextChunk, chunk);

                    var textModel = new TextSourceSearchModel()
                    {
                        File = file.Info,
                        Chunk = new ChunkReference()
                        {
                            Id = chunk.StableId,
                            StartLineNumber = chunkFile.Chunks[index].StartLineNumber
                        }
                    };

                    await AddAsync(SearchTypes.TextSource, textModel);
                }
            }
        }

        private void PopulateTextChunk(TextChunkSearchModel chunk)
        {
            chunk.PopulateContentIdAndSize();
        }

        private async Task AddDefinitionsAsync(IEnumerable<DefinitionSymbol> definitions, bool declared)
        {
            foreach (var definition in definitions)
            {
                if (definition.ExcludeFromSearch)
                {
                    // Definitions must be stored even if not contributing to search to allow
                    // other operations like tooltips/showing symbol name for find all references
                    // so we just set ExcludeFromDefaultSearch to true
                    definition.ExcludeFromDefaultSearch = true;
                }

                DefinitionSearchModel searchModel = new DefinitionSearchModel()
                {
                    Definition = definition,
                };
                await AddAsync(SearchTypes.Definition, searchModel,
                // If definition is declared in this code base, add it to declared def filter for use when boosting or searching
                // only definitions that have source associated with them
                new() { AdditionalStoredFilters = (declared ? FilterName.DeclaredDefinitions : FilterName.None) });

                if (declared)
                {
                    SdkFeatures.AfterDefinitionAddHandler.Value?.Invoke(searchModel);
                }
            }
        }

        private async Task AddPropertiesAsync(ISearchEntity entity, PropertyMap properties)
        {
            if (properties == null)
            {
                return;
            }

            foreach (var property in properties)
            {
                await AddAsync(SearchTypes.Property, new PropertySearchModel()
                {
                    Key = property.Key.ToDisplayString(),
                    Value = property.Value,
                    OwnerId = entity.StableId,
                });
            }
        }

        protected override async Task AddBoundSourceFileAsync(BoundSourceFile boundSourceFile)
        {
            var sourceFileInfo = boundSourceFile.SourceFile.Info;
            Logger.LogDiagnosticWithProvenance($"{sourceFileInfo.ProjectId}:{sourceFileInfo.ProjectRelativePath}");
            sourceFileInfo.CommitId ??= StoreInfo.Commit.CommitId;
            boundSourceFile.ApplySourceFileInfo();

            await AddTextFileAsync(
                boundSourceFile.SourceFile,
                addChunks: !boundSourceFile.Flags.HasFlag(BoundSourceFlags.DisableTextIndexing));

            var boundSourceModel = new BoundSourceSearchModel()
            {
                BindingInfo = boundSourceFile,
                File = boundSourceFile.SourceFile,
                CompressedClassifications = ClassificationListModel.CreateFrom(boundSourceFile.Classifications),
                Content = boundSourceFile.SourceFile.Content
            };

            CreateReferenceLists(boundSourceFile, boundSourceModel);

            await AddAsync(SearchTypes.BoundSource, boundSourceModel);

            //await AddAsync(SearchTypes.TextSource, textModel);

            await AddBoundSourceFileAssociatedDataAsync(boundSourceFile, boundSourceModel);
            Logger.LogDiagnosticWithProvenance($"[Bound#{boundSourceModel.Uid}] {sourceFileInfo.ProjectId}:{sourceFileInfo.ProjectRelativePath}");
        }

        private void CreateReferenceLists(BoundSourceFile boundSourceFile, BoundSourceSearchModel boundSourceModel)
        {
            var referenceLookup = boundSourceFile.References
                //.Where(r => !(r.Reference.ExcludeFromSearch))
                .ToLookup(r => r.Reference, CodeSymbol.SymbolEqualityComparer);

            foreach (var referenceGroup in referenceLookup)
            {
                var list = new SymbolReferenceList()
                {
                    Symbol = referenceGroup.Key
                };

                var spanList = referenceGroup.AsReadOnlyList();

                if (!Features.ColumnStoreReferenceInfo || referenceGroup.Count() < 10)
                {
                    // Small number of references, just store simple list
                    list.Spans = referenceGroup.Select(r => SharedReferenceInfoSpan.From(r)).ToList();
                }
                else
                {
                    list.CompressedSpans = SharedReferenceInfoSpanModel.CreateFrom(referenceGroup);
                }

                boundSourceModel.References.Add(list);
            }

            boundSourceModel.References.Sort(SymbolReferenceList.Comparer);
        }

        private async Task AddBoundSourceFileAssociatedDataAsync(
            BoundSourceFile boundSourceFile,
            BoundSourceSearchModel boundSourceModel)
        {
            if (boundSourceFile.ExcludeFromSearch)
            {
                return;
            }

            await AddPropertiesAsync(boundSourceModel, boundSourceFile.SourceFile.Info.Properties);

            await AddDefinitionsAsync(boundSourceFile.Definitions.Select(ds => ds.Definition), declared: true);

            foreach (var referenceGroup in boundSourceModel.References)
            {
                if (referenceGroup.Spans.All(r => r.Info.ExcludeFromSearch))
                {
                    continue;
                }

                var referenceModel = new ReferenceSearchModel()
                {
                    ReferenceKind = ReferenceKindSet.From(
                        referenceGroup.Spans
                            .Where(r => !r.Info.ExcludeFromSearch)
                            .Select(r => r.Info.ReferenceKind)),
                    References = referenceGroup,
                    Symbol = referenceGroup.Symbol,
                    RelatedDefinition = referenceGroup.Spans.Where(r => !r.Info.ExcludeFromSearch).Select(r => r.Info.RelatedDefinition),
                    FileInfo = boundSourceFile.SourceFile.Info
                };

                await AddAsync(SearchTypes.Reference, referenceModel);
            }
        }
    }
}