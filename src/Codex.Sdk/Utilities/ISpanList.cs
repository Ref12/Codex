using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Codex.Utilities
{
    public interface IIndexableSpans<T> : IIndexable<T>
    {
        ListSegment<T> GetSpans(int start, int length);
    }
}
