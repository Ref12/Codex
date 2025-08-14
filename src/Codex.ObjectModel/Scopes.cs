using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex
{
    public interface IRepoScopeEntity
    {
        /// <summary>
        /// The name of the repository containing the entity
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string RepositoryName { get; }
    }

    public interface IQualifierScopeEntity
    {
        /// <summary>
        /// The qualifier for the file or project
        /// The target framework/platform/os/configuration
        /// </summary>
        string Qualifier { get; }
    }

    public interface ICommitScopeEntity : IRepoScopeEntity
    {
        /// <summary>
        /// The unique identifier for this commit/changeset in version control
        /// (i.e. git commit hash or TFS changeset number)
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string CommitId { get; }
    }

    public interface IProjectScopeEntity : IRepoScopeEntity
    {
        /// <summary>
        /// The identifier of the project containing the entity
        /// </summary>
        [SearchBehavior(SearchBehavior.Sortword)]
        string ProjectId { get; }
    }

    public interface IRepoFileScopeEntity : IRepoScopeEntity
    {
        /// <summary>
        /// The repo relative path to the file
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string RepoRelativePath { get; }
    }

    public interface IProjectFileScopeEntity : IRepoFileScopeEntity, IProjectScopeEntity
    {
        /// <summary>
        /// The project relative path of the file
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string ProjectRelativePath { get; }
    }
}
