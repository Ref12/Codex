using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types
{
    /// <summary>
    /// High level storage operations
    /// </summary>
    public interface IStore<T>
    {
        // TODO: Generate preprocess
        // TODO: Update
        Task StoreAsync(IReadOnlyList<T> values);

        Task FinalizeAsync();
    }

    public partial interface IStore
    {
        Task InitializeAsync();

        Task FinalizeAsync();
    }
}
