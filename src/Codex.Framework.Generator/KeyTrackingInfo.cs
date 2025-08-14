using System.Collections.Immutable;

namespace Codex.Framework.Generator;

/// <summary>
/// Tracking information about values used as integer keys when serializing and deserializing
/// </summary>
public class KeyTrackingInfo
{
    public Dictionary<string, int> EntityPropertyNames { get; set; } = new();

    public ImmutableSortedDictionary<string, int> SortedEntityPropertyNames { get; set; } = ImmutableSortedDictionary<string, int>.Empty;

    public Dictionary<string, EnumKeyTrackingInfo> Enums { get; set; } = new();

    public class EnumKeyTrackingInfo
    {
        public Dictionary<string, int> FieldNames { get; set; } = new();
    }

}
