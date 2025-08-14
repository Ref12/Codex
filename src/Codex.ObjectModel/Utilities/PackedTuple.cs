using System.Runtime.InteropServices;

namespace Codex.Utilities
{
    public static class PackedTuple
    {
        public static PackedTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            return new(item1, item2);
        }

        public static PackedTuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            return new(item1, item2, item3);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public record struct PackedTuple<T1, T2>(T1 Item1, T2 Item2) 
        where T1 : unmanaged 
        where T2 : unmanaged;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public record struct PackedTuple<T1, T2, T3>(T1 Item1, T2 Item2, T3 Item3)
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    ;
}
