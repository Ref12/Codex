using Codex.Configuration;
using Codex.Utilities;

namespace Codex.Web.Common;

public record WebProgramArguments
{

    public required Uri RootUrl { get; set; }

    public required Uri StartUrl { get; set; }

    public IndexSourceLocation? IndexSource { get; set; }

    public string IndexSourceJsonUri { get; set; } = CodexConstants.CodexSourceFileName;

    public void Process()
    {
        RootUrl = RootUrl.WithoutQuery();
    }
}
