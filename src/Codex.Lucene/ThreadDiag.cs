using System.Runtime.CompilerServices;

namespace Codex.Lucene.Search
{
    public record ThreadDiag(int ThreadId, string Path, long Position)
    {
        public bool IsActive { get; set; } = true;
        public string Caller { get; private set; }
        public int Line { get; private set; }

        public string ExceptionText { get; set; }

        public void RegisterCaller([CallerMemberName]string caller = null, [CallerLineNumber]int line = 0)
        {
            Caller = caller;
            Line = line;
        }
    }
}
