using Codex.Utilities;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Build.Tasks
{
    public class CompilerArgumentsUtilities
    {
        public const string ProjectFilePrefix = "Project=";

        public enum CompilerKind
        {
            CSharp,
            VisualBasic
        }

        public static string[] GetCommandLineFromEventArgs(BuildEventArgs args, out CompilerKind language)
        {
            var task = args as TaskCommandLineEventArgs;
            language = default;
            if (task == null)
            {
                return null;
            }

            var name = task.TaskName;
            if (name != "Csc" && name != "Vbc")
            {
                return null;
            }

            var commandLine = task.CommandLine;

            language = GetCompilerKind(name);

            var commandLineArgs = CommandLineParser.SplitCommandLineIntoArguments(commandLine, removeHashComments: false);
            return commandLineArgs.ToArray();
        }

        public static CompilerKind GetCompilerKind(string name)
        {
            if (name.EqualsIgnoreCase("csc") || name == LanguageNames.CSharp)
            {
                return CompilerKind.CSharp;
            }    
            else if (name.EqualsIgnoreCase("vbc") || name == LanguageNames.VisualBasic)
            {
                return CompilerKind.VisualBasic;
            }

            return CompilerKind.CSharp;
        }

        public static string[] TrimCompilerExeFromCommandLine(IEnumerable<string> commandLineArgs, CompilerKind language)
        {
            commandLineArgs = language == CompilerKind.VisualBasic
                ? SkipCompilerExecutable(commandLineArgs, "vbc.exe", "vbc.dll")
                : SkipCompilerExecutable(commandLineArgs, "csc.exe", "csc.dll");

            return commandLineArgs.ToArray();
        }

        /// <summary>
        /// The argument list is going to include either `dotnet exec csc.dll` or `csc.exe`. Need 
        /// to skip past that to get to the real command line.
        /// </summary>
        internal static IEnumerable<string> SkipCompilerExecutable(IEnumerable<string> args, string exeName, string dllName)
        {
            var found = false;
            using (var e = args.GetEnumerator())
            {
                // The path to the executable is not escaped like the other command line arguments. Need
                // to skip until we see an exec or a path with the exe as the file name.
                while (e.MoveNext())
                {
                    if (PathUtil.Comparer.Equals(e.Current, "exec"))
                    {
                        if (e.MoveNext() && PathUtil.Comparer.Equals(Path.GetFileName(e.Current), dllName))
                        {
                            found = true;
                        }
                        break;
                    }
                    else if (e.Current.EndsWith(exeName, PathUtil.Comparison))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    while (e.MoveNext())
                    {
                        yield return e.Current;
                    }

                    yield break;
                }
            }

            if (!found)
            {
                foreach(var arg in args)
                {
                    yield return arg;
                }
            }
        }

        internal static class PathUtil
        {
            internal static readonly StringComparer Comparer = StringComparer.Ordinal;
            internal static readonly StringComparison Comparison = StringComparison.Ordinal;

            /// <summary>
            /// Replace the <paramref name="oldStart"/> with <paramref name="newStart"/> inside of
            /// <paramref name="filePath"/>
            /// </summary>
            internal static string ReplacePathStart(string filePath, string oldStart, string newStart)
            {
                var str = RemovePathStart(filePath, oldStart);
                return Path.Combine(newStart, str);
            }

            internal static string RemovePathStart(string filePath, string start)
            {
                var str = filePath.Substring(start.Length);
                if (str.Length > 0 && str[0] == Path.DirectorySeparatorChar)
                {
                    str = str.Substring(1);
                }

                return str;
            }
        }
    }
}
