using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codex.Search;

namespace Codex.Analysis
{
    public record StandardAnalysisDecorator(ICodexRepositoryStore InnerStore, ISourceControlInfoProvider SourceControl) 
        : WrapperCodexRepositoryStore(InnerStore)
    {
        public override Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
        {
            foreach (var file in files)
            {
                AddSourceControlData(file);
            }

            return base.AddBoundFilesAsync(files);
        }

        public void AddSourceControlData(BoundSourceFile sourceFile)
        {
            if (SourceControl is { } provider
                && provider.TryGetContentId(sourceFile.RepoRelativePath, out var contentId))
            {
                sourceFile.SourceFile.Info.SourceControlContentId = contentId.Value;
                sourceFile.SourceFile.Info.Properties[contentId.Key] = contentId.Value;
            }
        }
    }
}
