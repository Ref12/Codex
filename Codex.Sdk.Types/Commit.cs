using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types
{
    // TODO: These should be search types
    public interface ICommit
    {
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string CommitId { get; }

        string Description { get; }

        [SearchBehavior(SearchBehavior.Sortword)]
        DateTime DateUploaded { get; }

        [SearchBehavior(SearchBehavior.Sortword)]
        DateTime DateCommitted { get; }

        IReadOnlyList<string> Parents { get; }
    }

    public interface IBranch
    {
        string Name { get; }

        string Description { get; }

        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string CommitId { get; }
    }
}
