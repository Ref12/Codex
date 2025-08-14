using Codex.Utilities;

namespace Codex.Web.Common
{
    public interface IBytesRetriever
    {
        Task<long> GetLengthAsync(string url);

        Task<ReadOnlyMemory<byte>> GetBytesAsync(string url, LongExtent? extent = null);

        ReadOnlyMemory<byte> GetBytes(string url, LongExtent extent);
    }

    public record RemapBytesRetriever(IBytesRetriever Inner) : IBytesRetriever
    {
        public Dictionary<string, (string NewUrl, LongExtent? Range)> Map { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ReadOnlyMemory<byte> GetBytes(string url, LongExtent extent)
        {
            Remap(ref url, extent, out var newExtent);
            return Inner.GetBytes(url, newExtent.Value);
        }

        public Task<ReadOnlyMemory<byte>> GetBytesAsync(string url, LongExtent? extent = null)
        {
            Remap(ref url, extent, out extent);
            return Inner.GetBytesAsync(url, extent);
        }

        public async Task<long> GetLengthAsync(string url)
        {
            if (Remap(ref url, default, out var extent) && extent != null)
            {
                return extent.Value.Length;
            }

            return await Inner.GetLengthAsync(url);
        }

        private bool Remap(ref string url, LongExtent? extent, out LongExtent? newExtent)
        {
            if (Map.TryGetValue(url, out var newEntry))
            {
                newExtent = extent?.Shift(newEntry.Range?.Start ?? 0) ?? newEntry.Range;
                url = newEntry.NewUrl ?? url;
                return true;
            }
            else
            {
                newExtent = extent;
                return false;
            }
        }
    }
}
