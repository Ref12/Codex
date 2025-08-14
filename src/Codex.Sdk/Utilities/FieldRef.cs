namespace Codex.Utilities
{
    public static class FieldRef
    {
        public static FieldRef<TData, T> New<TData, T>(TData data, GetField<TData, T> getField)
        {
            return new(data, getField);
        }
    }

    public delegate ref T GetField<TData, T>(TData data);
    public record struct FieldRef<TData, T>(TData Data, GetField<TData, T> GetField);
}