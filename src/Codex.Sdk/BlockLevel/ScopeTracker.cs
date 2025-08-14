using System.Runtime.InteropServices;
using Codex.ObjectModel;
using Codex.Utilities.Serialization;

namespace Codex.Storage.BlockLevel;

public class ScopeTracker
{
    public const int MAX_SYMBOL_DEPTH = 64;
    private int[] depthVersions = new int[MAX_SYMBOL_DEPTH];

    public static void WriteLocalId(ref SpanWriter writer, int scopedLocalId, bool isStart, int symbolDepth)
    {
        writer.WriteInt32Compact((symbolDepth << 1) | (isStart ? 1 : 0));
        writer.Write(scopedLocalId);
    }

    private Dictionary<int, int> localIdMap = new Dictionary<int, int>();

    private int _localIdCursor = 0;
    private IClassificationSpan _lastSpan = null;

    public int GetLocalId(IClassificationSpan span)
    {
        if (span == null || span.LocalGroupId == 0) return 0;

        var localId = GetLocalId(span.LocalGroupId, _lastSpan != span);
        _lastSpan = span;
        return localId;
    }

    public int GetLocalId(int rawLocalId, bool canBeStart = true)
    {
        if (rawLocalId == 0) return 0;
        int initialId = rawLocalId;

        bool isStart = rawLocalId.HasFlag(1) && canBeStart;

        rawLocalId >>= 1;

        int result;
        if (isStart)
        {
            // Create local ids that can safely be replayed by always setting isStart=false 
            result = (++_localIdCursor) << 1;
            localIdMap[rawLocalId] = result;
            return result;
        }
        else if (localIdMap.TryGetValue(rawLocalId, out result))
        {
            return result;
        }
        else
        {
            // Normally this code path should not be hit, but in cases where replaying
            // local ids which have already been transformed, this will be hit and should
            // only return the original ids because isStart will always be false.
            return initialId;
        }
    }
}
