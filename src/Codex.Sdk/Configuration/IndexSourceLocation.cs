
using Codex.Storage;

namespace Codex.Configuration;

public record IndexSourceLocation
{
    /// <summary>
    /// The url or path of the index. Required.
    /// </summary>
    public required string Url { get; set; }

    public SnapshotTimeUtc Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Indicates the header which should be the same across index files. Generally for static sites
    /// on github pages or cloudflare, this should be set to 'Last-Modified'. Current 
    /// </summary>
    public string? ReloadHeader { get; set; }

    /// <summary>
    /// Gets whether entity files can trigger a reload
    /// </summary>
    public bool EntityFilesTriggerReload { get; set; }

    /// <summary>
    /// Indicates the interval to recheck the sources file for a change to the index location
    /// </summary>
    // TODO: Implement refresh logic
    public TimeSpanSetting RefreshInterval { get; set; }
}

public enum ReloadBehavior
{
    /// <summary>
    /// Index does not change. No reload.
    /// </summary>
    Immutable
}