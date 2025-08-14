using System.Runtime.CompilerServices;
using Codex.Utilities.Serialization;

namespace Codex.ObjectModel.Attributes
{
    /// <summary>
    /// Specifies the type of search behavior for a field.
    /// </summary>
    public enum SearchBehavior
    {
        /// <summary>
        /// No search behavior.
        /// </summary>
        None,

        /// <summary>
        /// Standard term search (exact match).
        /// </summary>
        Term,

        /// <summary>
        /// Search using a normalized keyword (case-insensitive, trimmed, etc.).
        /// </summary>
        NormalizedKeyword,

        /// <summary>
        /// Search using a sortword (normalized for sorting purposes).
        /// </summary>
        Sortword,

        /// <summary>
        /// Search using a hierarchical path (e.g., file or namespace paths).
        /// </summary>
        HierarchicalPath,

        /// <summary>
        /// Full-text search (tokenized, supports partial matches).
        /// </summary>
        FullText,

        /// <summary>
        /// Search using a prefix term (matches terms starting with the prefix).
        /// </summary>
        PrefixTerm,

        /// <summary>
        /// Search using a prefix of a short name (e.g., symbol short name).
        /// </summary>
        PrefixShortName,

        /// <summary>
        /// Search using a prefix of a full name (e.g., symbol full name).
        /// </summary>
        PrefixFullName,

        /// <summary>
        /// Search using a sort value (optimized for sorting).
        /// </summary>
        SortValue,
    }

    public record SearchBehaviorInfo(
        SearchBehavior? Behavior = null,
        bool CanSearchPartialTerm = false, 
        bool DisallowSummarizeFullTerm = false,
        bool PreferBinary = false,
        bool IsSymbolId = false,
        bool IsHashExcluded = false,
        bool IsStableId = false,
        bool LowCardinalityTermOptimization = false)
    {
        public bool TryGetBinaryValue(string value, out ValueArray<byte, T256> binaryValue)
        {
            binaryValue = ValueArrayLength.MaxCapacity;
            if (!PreferBinary) return false;

            if (IsSymbolId)
            {
                if (SymbolId.UnsafeCreateWithValue(value).TryGetBinaryValue(out var idValue))
                {
                    SpanWriter writer = binaryValue.GetValuesSpan();
                    writer.Write(idValue);
                    binaryValue.Length = writer.Position;
                    return true;
                }

                return false;
            }

            return false;
        }
    }
}