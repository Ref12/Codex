using Codex.ObjectModel;

namespace Codex.Search
{
    public interface ISourceControlInfoProvider : IDisposable
    {
        bool TryGetContentId(string repoRelativePath, out KeyValuePair<PropertyKey, string> sourceControlContentId);
    }
}
