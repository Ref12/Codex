using Codex.Storage;

namespace Codex;

public class CodexProgramBase
{
    static CodexProgramBase()
    {
    }

    public static void Initialize()
    {
        // No-op triggers static constructor if class is not already loaded
    }
}
