using System.Runtime.CompilerServices;
using Codex.Utilities;
using CodexTestBProject;
using CodexTestProject;

namespace Codex.Integration.Tests;

public interface ITestProject
{
    static abstract string ProjectDirectory { get; }

    static abstract string ProjectPath { get; }
}

public interface ITestProjectData
{
    string ProjectDirectory { get; }

    string ProjectPath { get; }

    string RepoName { get; }
    string Name { get; }
}

public class TestProjects
{
    public static TestProjectData<ProjectA> A { get; } = new();
    public static TestProjectData<ProjectB> B { get; } = new();
    public static TestProjectData<VBProject> VB { get; } = new();

    public record TestProjectData<T>([CallerMemberName] string Name = null) : ITestProjectData
        where T : ITestProject
    {
        public string ProjectDirectory => T.ProjectDirectory;
        public string ProjectPath => T.ProjectPath;

        public string RepoName { get; } = $"testproj/{Name}";
    }

    public class VBProject : ITestProject
    {
        public static string ProjectDirectory { get; } = Replace(TestProject.ProjectDirectory);

        public static string ProjectPath { get; } = Replace(TestProject.ProjectPath);

        public static string Replace(string s)
        {
            return s.ReplaceIgnoreCase("CodexTestProject", "VBCodexTestProject")
                .ReplaceIgnoreCase(".csproj", ".vbproj");
        }
    }

    public class ProjectA : TestProject, ITestProject
    {
    }

    public class ProjectB : TestBProject, ITestProject
    {
    }
}


