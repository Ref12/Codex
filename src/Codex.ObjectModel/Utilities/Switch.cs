namespace Codex.Utilities
{
    public record struct Switch<T>(T Primary, T Secondary)
    {
        public Switch<T> Swap() => new(Secondary, Primary);
    }

    public static class Switch
    {
        public static Switch<T> Create<T>(T primary, T secondary) => new(primary, secondary);

        public static Switch<T> Create<T>(Func<T> factory) => new(factory(), factory());
    }
}
