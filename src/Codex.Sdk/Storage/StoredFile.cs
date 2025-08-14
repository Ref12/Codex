using Codex.Sdk.Utilities;

namespace Codex.Storage;

public record StoredFile<T>(IAsyncObjectStorage Storage, string RelativePath)
    where T : class, new()
{
    public ValueTask<T> LoadAsync(AsyncOut<bool> exists = null)
    {
        return Storage.CreateOrLoadValueAsync<T>(RelativePath, exists);
    }

    public ValueTask<string> WriteAsync(T value)
    {
        var stream = new MemoryStream();
        JsonSerializationUtilities.SerializeEntityTo(value, stream, flags: JsonFlags.Indented);

        return Storage.WriteAsync(RelativePath, stream);
    }

    public static implicit operator StoredFile<T>(None f) => StoredFile.CreateNull<T>();
}

public class StoredFile
{
    public static None Null { get; }

    public static StoredFile<T> CreateNull<T>()
        where T : class, new()
    {
        return new StoredFile<T>(NullObjectStorage.Instance, "");
    }

}
