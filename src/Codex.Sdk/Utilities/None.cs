using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Utilities
{
    /// <summary>
    /// Empty struct for creating a ValueTask with no result
    /// </summary>
    public struct None
    {
        public static readonly None Value = new None();
    }
}
