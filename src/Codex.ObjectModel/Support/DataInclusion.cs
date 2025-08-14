using System;
using System.Collections.Generic;
using System.Text;

namespace Codex.ObjectModel.Attributes
{
    public enum DataInclusionOptions
    {
        /// <summary>
        /// Data should always be included
        /// </summary>
        None = 0,
        Definitions = 1,
        References = 1 << 1,
        Classifications = 1 << 2,
        SearchDefinitions = 1 << 3,
        SearchReferences = 1 << 4,
        Content = 1 << 5,
        All = Definitions | References | Classifications | SearchDefinitions | SearchReferences | Content,

        // Default does not include definitions since they can be queried lazily rather than eagerly retrieved.
        Default = References | Classifications | SearchDefinitions | SearchReferences | Content
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class DataInclusionAttribute : Attribute
    {
        public readonly DataInclusionOptions DataInclusion;

        public DataInclusionAttribute(DataInclusionOptions dataInclusion)
        {
            DataInclusion = dataInclusion;
        }
    }
}
