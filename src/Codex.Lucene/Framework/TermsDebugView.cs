using System.Diagnostics;
using Codex.Lucene.Framework;
using Lucene.Net.Index;

[assembly: DebuggerTypeProxy(typeof(TermsDebugView), Target = typeof(Terms))]
[assembly: DebuggerTypeProxy(typeof(TermsDebugView), Target = typeof(MultiTerms))]

namespace Codex.Lucene.Framework
{
    public class TermsDebugView(Terms terms)
    {
        public SetTermsEnum Terms { get; } = SetTermsEnum.Create(terms.GetEnumerator());
    }
}
