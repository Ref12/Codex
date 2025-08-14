using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Runtime.InteropServices;
using Codex.Storage.BlockLevel;
using Codex.Utilities;
using Codex.Utilities.Serialization;
using Microsoft.CodeAnalysis;

namespace Codex.Analysis.Managed;

using static IndexingUtilities;

public record LocalSymbolState()
{
    private static IUnifiedComparer<SymbolAnalysisState> ScopedNameSymbolStateComparer { get; } = new ComparerBuilder<SymbolAnalysisState>()
        .CompareByAfter(s => s.SymbolDepth.Value)
        .CompareByAfter(s => s.LocalName);

    private static IUnifiedComparer<string> UnifiedStringComparer { get; } = new ComparerBuilder<string>()
        .CompareByAfter(s => s);

    public List<SymbolAnalysisState> LocalSymbolStates { get; } = new List<SymbolAnalysisState>();

    public void AddLocalSymbolState(SymbolAnalysisState state)
    {
        Contract.Assert(state.LocalName != null);
        Contract.Assert(state.SymbolDepth != null);
        if (state.LocalSymbolStateIndex >= 0) return;

        state.LocalSymbolStateIndex = LocalSymbolStates.Count;
        LocalSymbolStates.Add(state);
    }

    public void ScopeClassificationLocalIds(IReadOnlyList<SymbolicClassificationSpan> spans)
    {
        var names = LocalSymbolStates.SelectList(s => s.LocalName);

        var scopedNameMapping = SymbolMapping.PopulateSymbolMap<string>(names, UnifiedStringComparer, static s => UnicodeHash(s));

        Span<int> localIdSpan = stackalloc int[2];

        SpanWriter writer = MemoryMarshal.AsBytes(localIdSpan);

        foreach (var state in LocalSymbolStates)
        {
            state.ScopedLocalId = scopedNameMapping[state.LocalName];
            Contract.Assert(state.ScopedLocalId > 0);

            localIdSpan.Clear();
            writer = MemoryMarshal.AsBytes(localIdSpan);
            ScopeTracker.WriteLocalId(ref writer, scopedLocalId: state.ScopedLocalId, isStart: true, symbolDepth: state.SymbolDepth.Value);
            state.StartLocalId = localIdSpan[0];

            localIdSpan.Clear();
            writer = MemoryMarshal.AsBytes(localIdSpan);
            ScopeTracker.WriteLocalId(ref writer, scopedLocalId: state.ScopedLocalId, isStart: false, symbolDepth: state.SymbolDepth.Value);
            state.AfterStartLocalId = localIdSpan[0];
        }

        var uniqueLocalIds = new HashSet<int>();

        foreach (var span in spans)
        {
            if (span.State is not { } state || state.SymbolDepth >= ScopeTracker.MAX_SYMBOL_DEPTH) continue;

            bool isStart = span.Start == state.MinSymbolStart;
            span.LocalGroupId = isStart ? state.StartLocalId : state.AfterStartLocalId;
            uniqueLocalIds.Add(span.LocalGroupId);
            span.SymbolDepth = 0;
            Contract.Assert(span.LocalGroupId > 0);
        }
    }

    public static void HeuristicNormalizeClassifications(BoundSourceFile sourceFile)
    {
        var localSymbolState = new LocalSymbolState();
        var content = sourceFile.SourceFile.Content;

        var symbolDepths = new Dictionary<int, SymbolAnalysisState>();

        var symbolClassifications = sourceFile.Classifications.SelectArray(cs => new SymbolicClassificationSpan(cs));
        sourceFile.Classifications = symbolClassifications;

        foreach (var item in sourceFile.GetLineClassifications())
        {
            if (item.Value.LocalGroupId > 0)
            {
                var state = symbolDepths.GetOrAdd(item.Value.LocalGroupId, 0, (k, _) => new SymbolAnalysisState());
                if (state.LocalName == null)
                {
                    state.LocalName = content.Substring(item.Start, item.Length);

                    // Use offset in line as symbol depth
                    state.SymbolDepth = item.Offset;
                    localSymbolState.AddLocalSymbolState(state);
                }
                else
                {
                    // Use offset in line as symbol depth
                    state.SymbolDepth = Math.Min(item.Offset, state.SymbolDepth.Value);
                }

                ((SymbolicClassificationSpan)item.Value).Associate(state);
            }
        }

        localSymbolState.ScopeClassificationLocalIds(symbolClassifications);
    }
}
