namespace Codex.Automation.Workflow;

public class AnalyzeSettings
{
    public string BuildCmdRelativePath { get; set; }
    public bool Build { get; set; } = true;
    public bool EncryptOutputs { get; set; } = true;
    public List<string> BuildFiles { get; set; }
    public List<string> AdditionalBuildFiles { get; set; }
    public List<string> AddArguments { get; set; } = new List<string>();
    public List<string> RemoveArguments { get; set; } = new List<string>();
    public string RepoRoot { get; set; }
    public List<string> PrependedPaths { get; set; } = new();

    public EnvMap EnvironmentVariables { get; set; } = new();

}