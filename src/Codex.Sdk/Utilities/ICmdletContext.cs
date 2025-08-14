using System.Collections;

namespace Codex.Utilities
{
    public interface ICmdletContext
    {
        void WriteObject(object sendToPipeline);

        void WriteObject(object sendToPipeline, bool enumerateCollection);

        void WriteObjects(IEnumerable sendToPipeline) => WriteObject(sendToPipeline, enumerateCollection: true);
    }

    public class NullCmdletContext : ICmdletContext
    {
        public static NullCmdletContext Instance { get; } = new();

        public void WriteObject(object sendToPipeline)
        {
        }

        public void WriteObject(object sendToPipeline, bool enumerateCollection)
        {
        }
    }
}
