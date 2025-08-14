using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Storage.BlockLevel;
using Codex.Utilities.Serialization;

namespace Codex.Sdk.Search
{
    // +TODO: Generate ASP.Net endpoint which handles all these calls. Potentially also implement
    // caller (i.e. WebApiCodex : ICodex)
    /// <summary>
    /// High level operations for codex 
    /// </summary>
    public interface ICodex 
    {
        [SearchMethod(CodexServiceMethod.Search)]
        Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments);

        [SearchMethod(CodexServiceMethod.FindAllRefs)]
        Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync(FindAllReferencesArguments arguments);

        /// <summary>
        /// Find definition for a symbol
        /// Usage: Documentation hover tooltip
        /// </summary>
        [SearchMethod(CodexServiceMethod.FindDef)]
        Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments);

        /// <summary>
        /// Find definition location for a symbol
        /// Usage: Go To Definition
        /// </summary>
        [SearchMethod(CodexServiceMethod.FindDefLocation)]
        Task<IndexQueryResponse<ReferencesResult>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments);

        [SearchMethod(CodexServiceMethod.GetSource)]
        Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments);

        [SearchMethod(CodexServiceMethod.GetProject)]
        Task<IndexQueryResponse<GetProjectResult>> GetProjectAsync(GetProjectArguments arguments);

        [SearchMethod(CodexServiceMethod.GetRepoHeads)]
        Task<IndexQueryHitsResponse<ICommit>> GetRepositoryHeadsAsync(GetRepositoryHeadsArguments arguments);
    }

    public static class CodexGlobals
    {
        public static string RepositoryScopeId { get; set; }
    }

    public static class CodexSearchExtensions
    {
        public static async Task<string> GetFirstDefinitionFilePath(this ICodex codex, string projectId, string symbolId)
        {
            var response = await codex.FindDefinitionLocationAsync(new FindDefinitionLocationArguments()
            {
                ProjectId = projectId,
                SymbolId = symbolId,
                FallbackFindAllReferences = false,
                MaxResults = 1
            });

            return (response.Error != null || response.Result.Total == 0) ? null : response.Result.Hits[0].File?.ProjectRelativePath;
        }

        public static T ThrowOnError<T>(this T response)
            where T : IndexQueryResponse
        {
            if (!string.IsNullOrEmpty(response.Error))
            {
                throw new Exception(response.Error);
            }

            return response;
        }
    }

    public record CodexArgumentsBase
    {
        /// <summary>
        /// The maximum number of results to return
        /// </summary>
        public int MaxResults { get; set; } = 100;

        public bool Debug { get; set; }
    }

    public record ContextCodexArgumentsBase : CodexArgumentsBase
    {
        public const string AllRepositoryScopeId = "_all";

        public RepoAccess? AccessLevel { get; set; }

        public RepoAccess? SecondaryAccessLevel { get; set; }

        /// <summary>
        /// Disables the scoping stored filter. NOTE: The visibility filter is still applied.
        /// </summary>
        public bool DisableStoredFilter { get; set; } = false;

        /// <summary>
        /// The id of the repository to which to scope search results
        /// </summary>
        public string RepositoryScopeId { get; set; } = CodexGlobals.RepositoryScopeId;

        /// <summary>
        /// The id of the project to which to scope search results
        /// </summary>
        public string ProjectScopeId { get; set; }

        /// <summary>
        /// The id of the repository referencing the symbol.
        /// NOTE: This is used to priority inter-repository matches over
        /// matches from outside the repository
        /// </summary>
        public string ReferencingRepositoryId { get; set; }

        /// <summary>
        /// The id of the project referencing the symbol.
        /// NOTE: This is used to priority inter-repository matches over
        /// matches from outside the repository
        /// </summary>
        public string ReferencingProjectId { get; set; }

        /// <summary>
        /// The id of the file referencing the symbol.
        /// NOTE: This is used to priority inter-repository matches over
        /// matches from outside the repository
        /// </summary>
        public string ReferencingFileId { get; set; }
    }

    public record GetRepositoryHeadsArguments : ContextCodexArgumentsBase
    {
        public GetRepositoryHeadsArguments()
        {
            MaxResults = 50_000;
        }
    }

    public record FindSymbolArgumentsBase : ContextCodexArgumentsBase
    {
        /// <summary>
        /// The symbol id of the symbol
        /// </summary>
        public SymbolIdArgument SymbolId { get; set; }

        /// <summary>
        /// The project id of the symbol
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Finds related symbols such as overridden or implemented members
        /// </summary>
        public virtual bool IncludeRelatedDefinitions { get; set; }
    }

    public record struct SymbolIdArgument(SymbolId Value) : IEqualityOperators<SymbolIdArgument, SymbolId, bool>
    {
        public static bool operator ==(SymbolIdArgument left, SymbolId right)
        {
            return left.Value == right;
        }

        public static bool operator ==(SymbolId left, SymbolIdArgument right)
        {
            return right.Value == left;
        }

        public static bool operator !=(SymbolIdArgument left, SymbolId right)
        {
            return left.Value != right;
        }

        public static bool operator !=(SymbolId left, SymbolIdArgument right)
        {
            return right.Value != left;
        }

        public static implicit operator string(SymbolIdArgument arg)
        {
            return arg.Value.Value;
        }

        public static implicit operator SymbolId(SymbolIdArgument arg)
        {
            return arg.Value;
        }

        public static implicit operator SymbolIdArgument(string value)
        {
            return new SymbolIdArgument(SymbolId.UnsafeCreateWithValue(value));
        }

        public static implicit operator SymbolIdArgument(SymbolId value)
        {
            return new SymbolIdArgument(value);
        }
    }

    public record FindDefinitionArguments : FindSymbolArgumentsBase
    {
        public FindDefinitionArguments()
        {
            MaxResults = 1;
        }
    }

    public record FindAllReferencesArguments : FindSymbolArgumentsBase
    {
        public ReferenceKind? ReferenceKind { get; set; }

        public ReferenceKindSet? GetFindAllReferenceKinds() => ReferenceKind?.FindAllReferenceKinds();

        public override bool IncludeRelatedDefinitions { get; set; } = true;

        public bool RequireLineTexts { get; set; } = true;

        public bool IsFallback { get; set; }

        public bool HasCandidateTypeForwardReferences { get; set; }

        public virtual bool IsFindDefinitionLocation() => false;

        public virtual bool ShouldGetLineTexts(IReadOnlyList<IReferenceSearchResult> hits) => RequireLineTexts;
    }

    public record FindDefinitionLocationArguments : FindAllReferencesArguments
    {
        public FindDefinitionLocationArguments()
        {
            ReferenceKind = ObjectModel.ReferenceKind.Definition;
            RequireLineTexts = false;
        }

        public override bool IsFindDefinitionLocation() => true;

        public bool FallbackFindAllReferences { get; set; } = true;

        public override bool ShouldGetLineTexts(IReadOnlyList<IReferenceSearchResult> hits)
        {
            return base.ShouldGetLineTexts(hits) || ShouldUseReferencesResult(hits);
        }

        public bool ShouldUseReferencesResult(IReadOnlyList<IReferenceSearchResult> hits)
        {
            return hits.Count > 1 || hits[0].ReferenceSpan.Reference.ReferenceKind != ObjectModel.ReferenceKind.Definition;
        }
    }

    public record SearchArguments : ContextCodexArgumentsBase
    {
        public string SearchString { get; set; }

        public bool AllowReferencedDefinitions { get; set; } = false;

        public bool FallbackToTextSearch { get; set; } = false;

        public bool TextSearch { get; set; } = false;

        public bool TestTermSearch { get; set; } = false;
    }

    public record GetProjectArguments : ContextCodexArgumentsBase
    {
        public string ProjectId { get; set; }

        public string ReferencedProjectId { get; set; }

        public AddressKind AddressKind { get; set; }
    }

    public class GetProjectResult
    {
        public DateTime DateUploaded { get; set; }

        public bool GenerateReferenceMetadata { get; set; }

        public IAnalyzedProjectInfo Project { get; set; }

        public AddressKind AddressKind { get; set; }

        public IReadOnlyList<IProjectReferenceSearchModel> ReferencingProjects { get; set; } = Array.Empty<IProjectReferenceSearchModel>();
    }

    public record GetSourceArguments : ContextCodexArgumentsBase
    {
        // TODO: Add argument for getting just text content

        public string RepositoryName  { get; set; }

        public string ProjectId { get; set; }

        public string ProjectRelativePath { get; set; }

        public int? StableId { get; set; }

        public bool DefinitionOutline { get; set; } = false;

        public static GetSourceArguments From(IProjectFileScopeEntity file)
        {
            return new GetSourceArguments()
            {
                RepositoryName = file.RepositoryName,
                ProjectId = file.ProjectId,
                ProjectRelativePath = file.ProjectRelativePath
            };
        }
    }

    public interface IFileSpanResult
    {
        [UseInterface]
        IProjectFileScopeEntity File { get; }
    }

    public interface IReferenceSearchResult : IFileSpanResult
    {
        [UseInterface]
        IReferenceSpan ReferenceSpan { get; }
    }

    public interface ITextLineSpanResult : IFileSpanResult
    {
        ITextLineSpan TextSpan { get; }
    }

    public interface ISearchResult
    {
        /// <summary>
        /// The text span for a text result
        /// </summary>
        ITextLineSpanResult TextLine { get; }

        /// <summary>
        /// The definition of the search result
        /// </summary>
        IDefinitionSymbol Definition { get; }
    }

    public struct SerializableTimeSpan
    {
        public long Ticks { get; set; }

        public SerializableTimeSpan(TimeSpan timespan)
        {
            Ticks = timespan.Ticks;
        }

        public TimeSpan AsTimeSpan()
        {
            return TimeSpan.FromTicks(Ticks);
        }

        public static implicit operator TimeSpan(SerializableTimeSpan value)
        {
            return value.AsTimeSpan();
        }

        public static implicit operator SerializableTimeSpan(TimeSpan value)
        {
            return new SerializableTimeSpan(value);
        }

        public override string ToString()
        {
            return AsTimeSpan().ToString();
        }
    }

    public class IndexQueryResponse
    {
        public object[] DebugObjects { get; set; }

        /// <summary>
        /// If the query failed, this will contain the error message
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// The raw query sent to the index server
        /// </summary>
        public List<string> RawQueries { get; set; }

        /// <summary>
        /// The spent executing the query
        /// </summary>
        public SerializableTimeSpan Duration { get; set; }

        /// <summary>
        /// The spent executing the query
        /// </summary>
        public SerializableTimeSpan ServerTime { get; set; }

        public override string ToString()
        {
            return $"Error: {Error}, Duration: {Duration}";
        }
    }

    public class IndexQueryResponse<T> : IndexQueryResponse
    {
        /// <summary>
        /// The results of the query
        /// </summary>
        public T Result { get; set; }

        public override string ToString()
        {
            return $"Result: {Result}, {base.ToString()}";
        }
    }

    [GeneratorExclude]
    public interface IIndexQueryHits
    {
        public long Total { get; }

        public int HitCount { get; }
    }

    public class IndexQueryHits<T> : IIndexQueryHits
    {
        private long total;

        /// <summary>
        /// The total number of results matching the query. 
        /// NOTE: This may be greater than the number of hits returned.
        /// </summary>
        public long Total
        {
            get => total == 0 ? HitCount : total;
            set
            {
                total = value;
            }
        }

        /// <summary>
        /// The results of the query
        /// </summary>
        public IReadOnlyList<T> Hits { get; set; } = new List<T>(0);

        public int HitCount => Hits?.Count ?? 0;

        public bool HasHits() => HitCount > 0;

        public void Merge(IndexQueryHits<T> other)
        {
            var hitsList = Hits.AsList();
            hitsList.AddRange(other.Hits);
            Hits = hitsList;
            Total += other.Total;
        }

        public override string ToString()
        {
            return $"Total: {Total}, {base.ToString()}";
        }
    }

    public class ReferencesResult : IndexQueryHits<IReferenceSearchResult>
    {
        public string SymbolDisplayName { get; set; }

        public FindAllReferencesArguments Arguments { get; set; }

        public string SymbolId { get; set; }

        public string ProjectId { get; set; }

        public IDefinitionSymbol Definition { get; set; }

        public ReferenceKind? ReferenceKind { get; set; }

        public List<RelatedDefinition> RelatedDefinitions { get; set; } = new List<RelatedDefinition>();

        public void Merge(ReferencesResult other)
        {
            SymbolDisplayName ??= other.SymbolDisplayName;
            SymbolId ??= other.SymbolId;
            Arguments ??= other.Arguments;
            ProjectId ??= other.ProjectId;
            ReferenceKind ??= other.ReferenceKind;
            RelatedDefinitions.AddRange(other.RelatedDefinitions);
            base.Merge(other);
        }
    }

    public record RelatedDefinition(IDefinitionSymbol Symbol, ReferenceKind ReferenceKind);

    public class IndexQueryHitsResponse<T> : IndexQueryResponse<IndexQueryHits<T>>
    {
    }
}
