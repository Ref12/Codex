using System.Collections.Immutable;

namespace Codex.Utilities;

public static class EnumData<T>
    where T : unmanaged, Enum
{
    public static T Max { get; }
    public static T Min { get; }

    public static ImmutableArray<T> Values { get; } = ImmutableArray.Create(Enum.GetValues<T>());

    static EnumData()
    {
        var comparer = Comparer<T>.Default;
        foreach ((var value, var index) in Enum.GetValues<T>().WithIndices())
        {
            Max = index == 0 ? value : comparer.Max(value, Max);
            Min = index == 0 ? value : comparer.Min(value, Min);
        }
    }
}